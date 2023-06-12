using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class Chess
{
	public ulong[,] pieces = new ulong[2, 6];
	public List<int>[,] piecesSer = new List<int>[2, 6];
	public ulong[] occupancyByColor = new ulong[2];
	public ulong occupancy = 0;

	public int sideToMove = 0; // 0 = white, 1 = black
	public bool[,] castlingRights = new bool[,] {{true, true}, {true, true}};
	public int enPassantSquare = -1;
	public int halfMoveClock = 0;
	public int fullMoveCounter = 1;

	public Dictionary<int, List<int>>[] possibleMoves = new Dictionary<int, List<int>>[6]; // only includes sideToMove
	
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



		UpdateOccupancy();
		UpdateSerialized();
		GeneratePossibleMoves();
		// GD.Print(String.Format("FEN\nCastling Rights: {4}\nSide to move: {0}\nEn passant: {1}\nHalfmove: {2}\nFullmove: {3}", sideToMove, enPassantSquare, halfMoveClock, fullMoveCounter, castlingRights[0, 0]));
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
			}
		}
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

		/* Updating Functions */
		UpdateOccupancy();
		UpdateSerialized();
		GeneratePossibleMoves();

		GD.Print(String.Format("Castling Rights: {4}\nSide to move: {0}\nEn passant: {1}\nHalfmove: {2}\nFullmove: {3}\n", sideToMove, enPassantSquare, halfMoveClock, fullMoveCounter, castlingRights[0, 0]));
	}

	public void GeneratePossibleMoves(){
		for (int pieceN = 0; pieceN < 6; pieceN++)
		{
			possibleMoves[pieceN] = new Dictionary<int, List<int>> {};

			foreach (int pieceIndex in piecesSer[sideToMove, pieceN])
			{
				ulong pieceMoves = GenerateMovesByIndex(pieceN, pieceIndex);
				List<int> pieceMovesList = new List<int> {};

				foreach (int targetSquare in g.Serialize(pieceMoves))
				{
					pieceMovesList.Add(targetSquare);
				}

				possibleMoves[pieceN].Add(pieceIndex, pieceMovesList);
			}
		}
	}

	public ulong GenerateMovesByIndex(int pieceN, int index){
		ulong pseudoLegalMoves = 0;

		switch (pieceN)
		{
			// KING
			case 0:
				pseudoLegalMoves |= g.kingAttacks[index];

				/* Detecting Castling */
				for (int sideN = 0; sideN < 2; sideN++)
				{
					bool hasCastlingRight = castlingRights[sideToMove, sideN];
					bool isPathClear = (occupancy & g.castlingMasks[sideToMove, sideN]) == 0;
					// REMINDER: ADD CHECK RULES AND ENEMY CONTROLLED RULES FOR CASTLING

					if (hasCastlingRight && isPathClear)
					{
						pseudoLegalMoves |= 1UL << g.castlingKingPos[sideToMove, sideN];
					}
				}
				break;
			
			// QUEEN
			case 1:
				for (int dir = 0; dir < 8; dir++)
				{
					pseudoLegalMoves |= GetBlockedRayAttack(index, dir);
				}

				break;
			
			// BISHOP
			case 2:
				for (int dir = 0; dir < 8; dir += 2)
				{
					pseudoLegalMoves |= GetBlockedRayAttack(index, dir);
				}

				break;
			
			// KNIGHT
			case 3:
				pseudoLegalMoves |= g.knightAttacks[index];
				break;
			
			// ROOK
			case 4:
				for (int dir = 1; dir < 8; dir += 2)
				{
					pseudoLegalMoves |= GetBlockedRayAttack(index, dir);
				}

				break;
			
			// PAWN
			case 5:
				pseudoLegalMoves |= g.pawnMoves[sideToMove, index] & ~occupancy; // single push

				if ((index / 8) == g.DoubleStartRank(sideToMove) // if pawn is in home rank
					&& pseudoLegalMoves != 0) // if pawn move is not blocked in the single push
				{
					pseudoLegalMoves |= 1UL << (index + 2 * g.SinglePush(sideToMove)); // double push
				}

				pseudoLegalMoves |= g.pawnAttacks[sideToMove, index] & occupancyByColor[1 - sideToMove];

				/* Detecting En Passant */
				bool isEnPassantAllowed = (g.pawnAttacks[sideToMove, index] >> enPassantSquare & 1UL) == 1;
				if (isEnPassantAllowed){
					pseudoLegalMoves |= 1UL << enPassantSquare;
				}

				break;
			
		}

		
		pseudoLegalMoves &= ~occupancyByColor[sideToMove];
		return pseudoLegalMoves;
	}

	public ulong GetBlockedRayAttack(int index, int dir)
	{
		ulong attacks = g.rayAttacks[index, dir];
		ulong blockers = attacks & occupancy;

		if (blockers != 0UL)
		{
			int nearestBlocker = g.BitScan(blockers, g.dirNums[dir] > 0); // Gotten by finding the LS1B or MS1B depending whether the direction is +ve or -ve
			attacks ^= g.rayAttacks[nearestBlocker, dir];
			// attacks &= ~(blockers & occupancyByColor[sideToMove]); // excludes same color blockers from target
		}

		return attacks;
	}
}
