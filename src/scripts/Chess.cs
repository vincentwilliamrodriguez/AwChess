using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public partial class Chess
{
	public ulong[,] pieces = new ulong[2, 6];
	public List<int>[,] piecesSer = new List<int>[2, 6];
	public int[,] piecesCount = new int[2, 6];
	public ulong[] occupancyByColor = new ulong[2];
	public ulong occupancy = 0;
	// public ulong enemyAttacks = 0;

	public int sideToMove = 0; // 0 = white, 1 = black
	public bool[,] castlingRights = new bool[,] {{true, true}, {true, true}};
	public int enPassantSquare = -1;
	public int halfMoveClock = 0;
	public int fullMoveCounter = 1;
	public bool isInCheck = false;
	public ulong checkingPieces = 0UL;
	public ulong pinnedPieces = 0UL;
	public Dictionary<int, ulong> pinnedMobility = new Dictionary<int, ulong> {};
	public int kingPos = -1;

	public Dictionary<int, ulong>[] possibleMoves = new Dictionary<int, ulong>[6]; // only includes sideToMove
	public int[] lastMove = new int[] {-1, -1};
	public int gameOutcome = -1; // -1 = ongoing, 0 = white won, 1 = black won, 2 = draw

	public Chess() {
		GD.Print("g awaw");
	}

	public void ImportFromFEN(string FEN) {
		// rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1

		string[] fields = FEN.Split(' ');

		/* Piece Placement*/
		string[] ranks = fields[0].Split('/');
		Array.Reverse(ranks); // converting from big endian to little endian

		int i = 0;
		foreach (string rank in ranks)
		{
			foreach (char c in rank)
			{
				if (int.TryParse(c.ToString(), out _)) // checks if c is a number
				{
					i += c - '0'; // converts c from char to int, then adds it to i
					continue;
				}

				int colorN = Char.IsLower(c) ? 1 : 0;
				int pieceN = Array.IndexOf(new char[] {'k', 'q', 'b', 'n', 'r', 'p'}, Char.ToLower(c)); // converts char to int based on piece
				
				pieces[colorN, pieceN] |= 1UL << i;
				i++;
			}
		}

		/* Side to move */
		sideToMove = Array.IndexOf(new char[] {'w', 'b'}, char.Parse(fields[1]));

		/* Castling rights */
		var castlingChar = new char[,] {{'K', 'Q'}, {'k', 'q'}};

		for (int colorN = 0; colorN < 2; colorN++) {
			for (int sideN = 0; sideN < 2; sideN++) {

				bool isCastlingAllowed = fields[2].Contains(castlingChar[colorN, sideN]); // checks if color + side is in the castling rights field
				castlingRights[colorN, sideN] = isCastlingAllowed;

			}
		}

		/* En passant target square */
		if (fields[3] != "-") {
			int file = Array.IndexOf(new char[] {'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'}, fields[3][0]);
			int rank = fields[3][1] - '1'; // subtracting 1 because rank counting starts from 0
			enPassantSquare = g.ToIndex(file, rank);
		}

		/* Halfmove Clock */
		halfMoveClock = Convert.ToInt32(fields[4]);

		/* Fullmove counter */
		fullMoveCounter = Convert.ToInt32(fields[5]);


		Update();
	}

	public void Update()
	{
		UpdateOccupancy();
		UpdateSerialized();
		GeneratePossibleMoves();
		CheckGameOutcome();
	}

	public void UpdateOccupancy() {
		occupancyByColor = new ulong[2];
		occupancy = 0;

		for (int colorN = 0; colorN < 2; colorN++)
		{
			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				occupancyByColor[colorN] |= pieces[colorN, pieceN];
			}

			occupancy |= occupancyByColor[colorN];
		}
	}

	public void UpdateSerialized(){
		for (int colorN = 0; colorN < 2; colorN++)
		{
			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				piecesSer[colorN, pieceN] = g.Serialize(pieces[colorN, pieceN]);
				piecesCount[colorN, pieceN] = piecesSer[colorN, pieceN].Count;
			}
		}
	}

	public void CheckGameOutcome()
	{
		if (flattenPossibleMoves().Count > 0)
		{

			bool insufficientMaterial = piecesCount.Cast<int>().Sum() == 2;
			bool fiftyMoveRule = halfMoveClock >= 100;

			if (insufficientMaterial || fiftyMoveRule)
			{
				gameOutcome = 2; // draw
			}
			else
			{
				gameOutcome = -1; // ongoing
			}
		}
		else
		{
			if (isInCheck)
			{
				gameOutcome = 1 - sideToMove; // checkmate
			}
			else
			{
				gameOutcome = 2; // stalemate
			}
		}

		GD.Print("Game outcome: ", gameOutcome);
	}

	public int FindPieceN(int index) {
		for (int colorN = 0; colorN < 2; colorN++)
		{
			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				bool isPieceOfTarget = (pieces[colorN, pieceN] >> index & 1UL) == 1;
				if (isPieceOfTarget)
				{
					return pieceN;
				}
			}
		}
		
		return -1;
	}

	public int FindColorN(int index) {
		for (int colorN = 0; colorN < 2; colorN++)
		{
			bool isColorOfTarget = (occupancyByColor[colorN] >> index & 1UL) == 1;
			if (isColorOfTarget) {
				return colorN;
			}
		}

		return -1;
	}

	public void MakeMove(int startIndex, int endIndex) {
		/* Updating Pieces */
		int endPieceN = -1;
		bool isWhitesTurn = sideToMove == 0;

		if ((occupancy >> endIndex & 1UL) == 1)
		{
			endPieceN = FindPieceN(endIndex);
			pieces[1 - sideToMove, endPieceN] &= ~(1UL << endIndex);
		}

		int startPieceN = FindPieceN(startIndex);
		pieces[sideToMove, startPieceN] &= ~(1UL << startIndex);
		pieces[sideToMove, startPieceN] |= 1UL << endIndex;

		/* Enforcing Promotion */
		if (startPieceN == 5 && (endIndex / 8) == g.promotionRank[sideToMove])
		{
			pieces[sideToMove, 5] &= ~(1UL << endIndex);
			pieces[sideToMove, 1] |= 1UL << endIndex; // only queen promotion is available for now
		}

		/* Enforcing Castling */
		if (startPieceN == 0 && (startIndex % 8) == 4)
		{
			for (int sideN = 0; sideN < 2; sideN++)
			{
				if (endIndex == g.castlingKingPos[sideToMove, sideN]) // if king went queenside or kingside
				{
					pieces[sideToMove, 4] &= ~(1UL << g.castlingRookPosFrom[sideToMove, sideN]);
					pieces[sideToMove, 4] |= 1UL << g.castlingRookPosTo[sideToMove, sideN];
				}
			}
		}

		/* Updating Castling Rights */
		if (startPieceN == 0) // if king moved
		{
			castlingRights[sideToMove, 0] = false;
			castlingRights[sideToMove, 1] = false;
		}

		if (startPieceN == 4 || endPieceN == 4) // if rook moved or got captured
		{
			if (startIndex == 0 || startIndex == 56) // if queen's rook moved
			{
				castlingRights[sideToMove, 0] = false;
			}
			
			if (startIndex == 7 || startIndex == 63) // if king's rook moved
			{
				castlingRights[sideToMove, 1] = false;
			}

			if (endIndex == 0 || endIndex == 56) // if queen's rook got captured
			{
				castlingRights[1 - sideToMove, 0] = false;
			}
			
			if (endIndex == 7 || endIndex == 63) // if king's rook got captured
			{
				castlingRights[1 - sideToMove, 1] = false;
			}
		}
		
		
		/* Enforcing En Passant */
		if (endIndex == enPassantSquare && startPieceN == 5)
		{
			int targetOfEnPassant = endIndex + g.SinglePush(1 - sideToMove);
			pieces[1 - sideToMove, 5] &= ~(1UL << targetOfEnPassant);
		}

		/* Updating En Passant */
		if (startPieceN == 5 && // if pawn moved
			((startIndex / 8) == g.DoubleStartRank(sideToMove) && 
			 (endIndex / 8) == g.DoubleEndRank(sideToMove))) // if pawn double moved
		{
			enPassantSquare = endIndex - (isWhitesTurn ? 8 : -8);
		}
		else
		{
			enPassantSquare = -1;
		}
		
		/* Updating Clocks */
		if (endPieceN != -1 || startPieceN == 5)
		{
			halfMoveClock = 0;
		}
		else
		{
			halfMoveClock++;
		}
		
		if (!isWhitesTurn)
		{
			fullMoveCounter++;
		}

		/* Updating Side To Move */
		sideToMove = 1 - sideToMove;
		lastMove[0] = startIndex;
		lastMove[1] = endIndex;

		/* Updating Functions */
		Update();

		// GD.Print(String.Format("Castling Rights: {4}\nSide to move: {0}\nEn passant: {1}\nHalfmove: {2}\nFullmove: {3}\n", sideToMove, enPassantSquare, halfMoveClock, fullMoveCounter, castlingRights[0, 0]));
	}

	public void GeneratePossibleMoves(){
		// enemyAttacks = 0UL;
		kingPos = g.BitScan(pieces[sideToMove, 0]);
		isInCheck = IsKingInCheck(kingPos, sideToMove);

		if (isInCheck)
			checkingPieces = GetCheckingPieces(kingPos, sideToMove);
		else
			checkingPieces = 0UL;
		
		pinnedMobility = new Dictionary<int, ulong> {};
		pinnedPieces = GetPinnedPieces();

		for (int pieceN = 0; pieceN < 6; pieceN++)
		{
			possibleMoves[pieceN] = new Dictionary<int, ulong> {};

			foreach (int pieceIndex in piecesSer[sideToMove, pieceN])
			{
				ulong pieceMoves = GenerateMovesByIndex(pieceN, pieceIndex, sideToMove);
				possibleMoves[pieceN].Add(pieceIndex, pieceMoves);
			}

			// /* Enemy Attacks Bitboard */
			// foreach (int pieceIndex in piecesSer[1 - sideToMove, pieceN])
			// {
			// 	ulong pieceMoves = GenerateMovesByIndex(pieceN, pieceIndex, 1 - sideToMove);
			// 	enemyAttacks |= pieceMoves;
			// }
		}
	}

	public List<int[]> flattenPossibleMoves()
	{
		List<int[]> flattenedMoves = new List<int[]> {};

		foreach (var pieceType in possibleMoves)
		{
			foreach (var piece in pieceType.Keys.ToList())
			{
				foreach (var move in g.Serialize(pieceType[piece]))
				{
					flattenedMoves.Add(new int[2] {piece, move});
				}
			}
		}

		return flattenedMoves;
	}

	public ulong GenerateMovesByIndex(int pieceN, int index, int colorN){

		/* GENERATING (MOSTLY) PSEUDO-LEGAL MOVES */
		ulong pseudoLegalMoves = 0;

		switch (pieceN)
		{
			// KING
			case 0:
				pseudoLegalMoves |= g.kingAttacks[index];
				pseudoLegalMoves &= ~occupancyByColor[colorN];

				/* Detecting Castling */
				for (int sideN = 0; sideN < 2; sideN++)
				{
					ulong path = g.castlingMasks[colorN, sideN];
					bool hasCastlingRight = castlingRights[colorN, sideN];
					bool isPathClear = (occupancy & path) == 0;

					if (!isInCheck && hasCastlingRight && isPathClear)
					{
						bool isPathSafe = true;
						
						foreach (int square in g.Serialize(path))
						{
							if (IsKingInCheck(square, colorN))
							{
								isPathSafe = false;
								break;
							}
						}

						if (isPathSafe)
						{
							pseudoLegalMoves |= 1UL << g.castlingKingPos[colorN, sideN];
						}
					}
				}

				/* Filtering out illegal moves (king stepping to attacked squares) */
				foreach (int move in g.Serialize(pseudoLegalMoves))
				{
					if (IsKingInCheck(move, colorN))
					{
						pseudoLegalMoves &= ~(1UL << move);
					}
				}

				break;
			
			// QUEEN
			case 1:
				pseudoLegalMoves |= GenerateRookAttacks(index) | GenerateBishopAttacks(index);
				pseudoLegalMoves &= ~occupancyByColor[colorN];
				break;
			
			// BISHOP
			case 2:
				pseudoLegalMoves |= GenerateBishopAttacks(index);
				pseudoLegalMoves &= ~occupancyByColor[colorN];
				break;
			
			// KNIGHT
			case 3:
				pseudoLegalMoves |= g.knightAttacks[index];
				pseudoLegalMoves &= ~occupancyByColor[colorN];
				break;
			
			// ROOK
			case 4:
				pseudoLegalMoves |= GenerateRookAttacks(index);
				pseudoLegalMoves &= ~occupancyByColor[colorN];
				break;
			
			// PAWN
			case 5:
				pseudoLegalMoves |= g.pawnMoves[colorN, index] & ~occupancy; // single push

				if ((index / 8) == g.DoubleStartRank(colorN) // if pawn is in home rank
					&& pseudoLegalMoves != 0) // if pawn move is not blocked in the single push
				{
					pseudoLegalMoves |= 1UL << (index + 2 * g.SinglePush(colorN)) & ~occupancy; // double push
				}

				pseudoLegalMoves |= g.pawnAttacks[colorN, index] & occupancyByColor[1 - colorN];

				/* Detecting En Passant */
				bool isEnPassantAllowed = (g.pawnAttacks[colorN, index] >> enPassantSquare & 1UL) == 1;
				if (isEnPassantAllowed){
					int enPassantTarget = enPassantSquare + g.SinglePush(1 - sideToMove);
					ulong tOccupancy = occupancy & ~(1UL << enPassantTarget);  // "remove" en passant target from board

					ulong enemiesAfterEnPassant = GenerateRookXRay(occupancyByColor[sideToMove],
																   tOccupancy,
																   kingPos);
					enemiesAfterEnPassant &= pieces[1 - sideToMove, 4] |
											 pieces[1 - sideToMove, 1];
					enemiesAfterEnPassant &= ~occupancyByColor[sideToMove];
					
					if (enemiesAfterEnPassant == 0)
					{
						pseudoLegalMoves |= 1UL << enPassantSquare;
					}
				}

				break;
			
		}

		/* GENERATING LEGAL MOVES */
		ulong legalMoves = pseudoLegalMoves;

		/* Absolutely Pinned Pieces */
		if ((pinnedPieces >> index & 1UL) == 1) // if piece is pinned
		{
			legalMoves &= pinnedMobility[index];
		}

		/* Capturing and Blocking Checking Pieces */
		if (isInCheck & !(pieceN == 0)) // not including king since its moves have already been filtered
		{
			if (g.Serialize(checkingPieces).Count == 1)
			{
				ulong capturingMoves = legalMoves & checkingPieces;

				int checkingPieceIndex = g.BitScan(checkingPieces);
				ulong checkingPath = g.inBetween[kingPos, checkingPieceIndex];
				ulong blockingMoves = legalMoves & checkingPath;

				legalMoves &= capturingMoves | blockingMoves;
			}
			else
			{
				legalMoves = 0UL; // can only capture or block during single checks
			}
		}
		
		return legalMoves;
	}

	public ulong GenerateRookAttacks(int index, bool checking = false)
	{
		ulong output = 0UL;

		for (int dir = 1; dir < 8; dir += 2)
		{
			output |= GetBlockedRayAttack(index, dir, checking);
		}

		return output;
	}

	public ulong GenerateBishopAttacks(int index, bool checking = false)
	{
		ulong output = 0UL;

		for (int dir = 0; dir < 8; dir += 2)
		{
			output |= GetBlockedRayAttack(index, dir, checking);
		}

		return output;
	}

	public ulong GetBlockedRayAttack(int index, int dir, bool checking = false)
	{
		ulong attacks = g.rayAttacks[index, dir];
		ulong blockers = attacks & occupancy;

		if (checking)
			blockers &= ~(1UL << kingPos); // removing king from blockers to avoid check problems

		if (blockers != 0UL)
		{
			int nearestBlocker = g.BitScan(blockers, g.dirNums[dir] > 0); // Gotten by finding the LS1B or MS1B depending whether the direction is +ve or -ve
			attacks ^= g.rayAttacks[nearestBlocker, dir];
		}

		return attacks;
	}

	public ulong GetXRayAttack(ulong blockers, ulong tOccupancy, int index, int dir)
	{
		ulong attacks = g.rayAttacks[index, dir];
		blockers &= attacks;
		tOccupancy &= attacks;
		if (blockers == 0UL) {return 0UL;}

		int nearestBlocker = g.BitScan(blockers, g.dirNums[dir] > 0);
		int secondNearestBlocker = g.BitScan(tOccupancy & ~(1UL << nearestBlocker), g.dirNums[dir] > 0);

		if (secondNearestBlocker == -1) {return g.rayAttacks[nearestBlocker, dir];}
		
		return g.inBetween[nearestBlocker, secondNearestBlocker];
	}

	public ulong GenerateRookXRay(ulong blockers, ulong tOccupancy, int index)
	{
		ulong output = 0UL;

		for (int dir = 1; dir < 8; dir += 2)
		{
			output |= GetXRayAttack(blockers, tOccupancy, index, dir);
		}

		return output;
	}

	public ulong GenerateBishopXRay(ulong blockers, ulong tOccupancy, int index)
	{
		ulong output = 0UL;

		for (int dir = 0; dir < 8; dir += 2)
		{
			output |= GetXRayAttack(blockers, tOccupancy, index, dir);
		}

		return output;
	}


	public bool IsKingInCheck(int kingIndex, int colorN)
	{
		ulong enemyPawns = pieces[1 - colorN, 5];
		ulong kingAsPawn = g.pawnAttacks[colorN, kingIndex];
		if ((enemyPawns & kingAsPawn) != 0UL) {return true;}

		ulong enemyKnights = pieces[1 - colorN, 3];
		ulong kingAsKnight = g.knightAttacks[kingIndex];
		if ((enemyKnights & kingAsKnight) != 0UL)  {return true;}

		ulong enemyRQ = pieces[1 - colorN, 4] | 
						pieces[1 - colorN, 1];
		ulong kingAsRook = GenerateRookAttacks(kingIndex, true);
		if ((enemyRQ & kingAsRook) != 0UL)  {return true;}

		ulong enemyBQ = pieces[1 - colorN, 2] | 
						pieces[1 - colorN, 1];
		ulong kingAsBishop = GenerateBishopAttacks(kingIndex, true);
		if ((enemyBQ & kingAsBishop) != 0UL)  {return true;}

		ulong enemyKing = pieces[1 - colorN, 0];
		ulong kingAsKing = g.kingAttacks[kingIndex];
		if ((enemyKing & kingAsKing) != 0UL)  {return true;}

		return false;
	}
	
	public ulong GetCheckingPieces(int kingIndex, int colorN)
	{
		ulong enemyPawns = pieces[1 - colorN, 5];
		ulong kingAsPawn = g.pawnAttacks[colorN, kingIndex];

		ulong enemyKnights = pieces[1 - colorN, 3];
		ulong kingAsKnight = g.knightAttacks[kingIndex];

		ulong enemyRQ = pieces[1 - colorN, 4] | 
						pieces[1 - colorN, 1];
		ulong kingAsRook = GenerateRookAttacks(kingIndex, true);

		ulong enemyBQ = pieces[1 - colorN, 2] | 
						pieces[1 - colorN, 1];
		ulong kingAsBishop = GenerateBishopAttacks(kingIndex, true);

		return (enemyPawns & kingAsPawn) |
			   (enemyKnights & kingAsKnight) |
			   (enemyRQ & kingAsRook) |
			   (enemyBQ & kingAsBishop);
	}

	public ulong GetPinnedPieces()
	{
		ulong pinned = 0UL;
		ulong pinners = GenerateRookXRay(occupancyByColor[sideToMove], // own pieces as nearest blocker
										 occupancy, // all pieces as second nearest blocker
										 kingPos);
		pinners &= pieces[1 - sideToMove, 4] | // get opponent's rooks
				   pieces[1 - sideToMove, 1];  // get opponent's queens
		pinners &= ~occupancyByColor[sideToMove];

		foreach (int pinner in g.Serialize(pinners))
		{
			ulong tPinned = g.inBetween[kingPos, pinner] & occupancyByColor[sideToMove]; // own pieces between king and pinner
			pinned |= tPinned;

			foreach (int pinnedIndex in g.Serialize(tPinned))
			{
				if (!pinnedMobility.ContainsKey(pinnedIndex))
				{
					pinnedMobility.Add(pinnedIndex, g.inBetween[kingPos, pinner]);
				}
			}
		}


		pinners = GenerateBishopXRay(occupancyByColor[sideToMove],
										 occupancy,
										 kingPos);
		pinners &= pieces[1 - sideToMove, 2] | // get opponent's bishops
				   pieces[1 - sideToMove, 1];  // get opponent's queens
		pinners &= ~occupancyByColor[sideToMove];

		foreach (int pinner in g.Serialize(pinners))
		{
			ulong tPinned = g.inBetween[kingPos, pinner] & occupancyByColor[sideToMove];
			pinned |= tPinned;

			foreach (int pinnedIndex in g.Serialize(tPinned))
			{
				if (!pinnedMobility.ContainsKey(pinnedIndex))
				{
					pinnedMobility.Add(pinnedIndex, g.inBetween[kingPos, pinner]);
				}
			}
		}

		return pinned;
	}
}
