using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


public struct Board
{
	public ulong[,] pieces = new ulong[2, 6];
	public List<int>[,] piecesSer = new List<int>[2, 6];
	public int[,] piecesCount = new int[2, 6];
	public ulong[] occupancyByColor = new ulong[2];
	public ulong occupancy = 0;

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

	public Dictionary<int, ulong>[] possibleMovesBB = new Dictionary<int, ulong>[6]; // only includes sideToMove
	public List<Move> possibleMoves = new List<Move> {};
	public Move lastMove = new Move(-1, -1, -1);
	public int gameOutcome = -1; // -1 = ongoing, 0 = white won, 1 = black won, 2 = draw
	public ulong zobristKey = 0;
	public List<ulong> zobristHistory = new List<ulong> {};

	public bool[,] pawnFileSet = new bool[2,8];
	public bool isEndgame = false;

	public Board() {}

	public Board Clone()
	{
		Board clone = new Board();
		clone.pieces = pieces;
		clone.piecesSer = piecesSer;
		clone.piecesCount = piecesCount;
		clone.occupancyByColor = occupancyByColor;
		clone.occupancy = occupancy;

		clone.sideToMove = sideToMove;
		clone.castlingRights = castlingRights.Clone() as bool[,];
		clone.enPassantSquare = enPassantSquare;
		clone.halfMoveClock = halfMoveClock;
		clone.fullMoveCounter = fullMoveCounter;
		clone.isInCheck = isInCheck;
		clone.checkingPieces = checkingPieces;
		clone.pinnedPieces = pinnedPieces;
		clone.pinnedMobility = pinnedMobility;
		clone.kingPos = kingPos;

		clone.possibleMovesBB = possibleMovesBB;
		clone.lastMove = lastMove;
		clone.gameOutcome = gameOutcome;
		clone.zobristKey = zobristKey;
		clone.zobristHistory = zobristHistory.ToList();

		clone.pawnFileSet = pawnFileSet;
		clone.isEndgame = isEndgame;

		return clone;
	}
}

public struct Move
{
	public int pieceN = -1;
	public int start = -1;
	public int end = -1;
	public int promotionPiece = -1;
	public int capturedPiece = -1;
	public bool isEnPassant = false;

	public Move(int pieceN = -1, int start = -1, int end = -1, int promotionPiece = -1, int capturedPiece = -1)
	{
		this.pieceN = pieceN;
		this.start = start;
		this.end = end;
		this.promotionPiece = promotionPiece;
		this.capturedPiece = capturedPiece;
	}
}

public partial class Chess
{
	public Board b = new Board();

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
				int pieceN = Array.IndexOf(g.piecesArray, Char.ToLower(c)); // converts char to int based on piece
				
				b.pieces[colorN, pieceN] |= 1UL << i;
				i++;
			}
		}

		/* Side to move */
		b.sideToMove = Array.IndexOf(new char[] {'w', 'b'}, char.Parse(fields[1]));

		/* Castling rights */
		var castlingChar = new char[,] {{'Q', 'K'}, {'q', 'k'}};

		for (int colorN = 0; colorN < 2; colorN++) {
			for (int sideN = 0; sideN < 2; sideN++) {
				bool isCastlingAllowed = fields[2].Contains(castlingChar[colorN, sideN]); // checks if color + side is in the castling rights field
				b.castlingRights[colorN, sideN] = isCastlingAllowed;
			}
		}
		
		/* En passant target square */
		if (fields[3] != "-") {
			int file = Array.IndexOf(g.fileArray, fields[3][0]);
			int rank = fields[3][1] - '1'; // subtracting 1 because rank counting starts from 0
			b.enPassantSquare = g.ToIndex(file, rank);
		}

		/* Halfmove Clock */
		b.halfMoveClock = Convert.ToInt32(fields[4]);

		/* Fullmove Counter */
		b.fullMoveCounter = Convert.ToInt32(fields[5]);

		/* Update Variables */
		Update();

		/* Zobrist Key Initialization */
		b.zobristKey = GetZobristKey();
		b.zobristHistory.Add(b.zobristKey);
	}

	public void Update()
	{
		UpdateOccupancy();
		UpdateSerialized();
		GeneratePossibleMoves();
		flattenPossibleMoves();
		CheckGameOutcome();
	}

	public void UpdateOccupancy() {
		b.occupancyByColor = new ulong[2];
		b.occupancy = 0;

		for (int colorN = 0; colorN < 2; colorN++)
		{
			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				b.occupancyByColor[colorN] |= b.pieces[colorN, pieceN];
			}

			b.occupancy |= b.occupancyByColor[colorN];
		}
	}

	public void UpdateSerialized(){
		b.pawnFileSet = new bool[2, 8];

		for (int colorN = 0; colorN < 2; colorN++)
		{
			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				b.piecesSer[colorN, pieceN] = g.Serialize(b.pieces[colorN, pieceN]);
				b.piecesCount[colorN, pieceN] = b.piecesSer[colorN, pieceN].Count;
				
				/* Pawn File Occupancy */
				if (pieceN == 5)
				{
					foreach (int pawnIndex in b.piecesSer[colorN, pieceN])
					{
						b.pawnFileSet[colorN, pawnIndex % 8] = true;
					}
				}
			}
		}

		/* Endgame Check */
		if (b.piecesCount[0, 1] + b.piecesCount[1, 1] == 0) // if both sides don't have a queen
			b.isEndgame = true;

		else if (b.piecesCount[0, 2] + b.piecesCount[0, 3] <= 1 &&	// if white has 1 minor piece at most
				 b.piecesCount[1, 2] + b.piecesCount[1, 3] <= 1)	// if black has 1 minor piece at most
			b.isEndgame = true;
			
	}

	public void CheckGameOutcome()
	{
		if (b.possibleMoves.Count > 0)
		{

			bool insufficientMaterial = b.piecesCount.Cast<int>().Sum() == 2;
			bool fiftyMoveRule = b.halfMoveClock >= 100;
			bool threeFoldRepetition = (b.zobristHistory.Where(key => key == b.zobristKey).Count()) >= 3;

			if (insufficientMaterial || fiftyMoveRule || threeFoldRepetition)
			{
				b.gameOutcome = 2; // draw
			}
			else
			{
				b.gameOutcome = -1; // ongoing
			}
		}
		else
		{
			if (b.isInCheck)
			{
				b.gameOutcome = 1 - b.sideToMove; // checkmate
			}
			else
			{
				b.gameOutcome = 2; // stalemate
			}
		}
	}

	public int FindPieceN(int index) {
		for (int colorN = 0; colorN < 2; colorN++)
		{
			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				bool isPieceOfTarget = (b.pieces[colorN, pieceN] >> index & 1UL) == 1;
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
			bool isColorOfTarget = (b.occupancyByColor[colorN] >> index & 1UL) == 1;
			if (isColorOfTarget) {
				return colorN;
			}
		}

		return -1;
	}

	public void MakeMove(Move move) {
		int startIndex = move.start;
		int endIndex = move.end;
		int promotionPiece = move.promotionPiece;

		/* Updating Pieces */
		bool isWhitesTurn = b.sideToMove == 0;

		if ((b.occupancy >> endIndex & 1) == 1UL) // if end index has a piece
		{
			b.pieces[1 - b.sideToMove, move.capturedPiece] &= ~(1UL << endIndex); // removes captured enemy piece from board
			b.zobristKey ^= g.zobristNumsPos[1 - b.sideToMove, move.capturedPiece, endIndex];
		}

		int startPieceN = move.pieceN;
		b.pieces[b.sideToMove, startPieceN] &= ~(1UL << startIndex); // removes moving piece from start position
		b.pieces[b.sideToMove, startPieceN] |= 1UL << endIndex; // places moving piece to end position
		b.zobristKey ^= g.zobristNumsPos[b.sideToMove, startPieceN, startIndex];
		b.zobristKey ^= g.zobristNumsPos[b.sideToMove, startPieceN, endIndex];

		/* Enforcing Promotion */
		if (g.IsPromotion(b.sideToMove, startPieceN, endIndex))
		{
			b.pieces[b.sideToMove, 5] &= ~(1UL << endIndex); // removes pawn from promotion square
			b.pieces[b.sideToMove, promotionPiece] |= 1UL << endIndex; // places promotion piece to promotion square
			b.zobristKey ^= g.zobristNumsPos[b.sideToMove, 5, endIndex];
			b.zobristKey ^= g.zobristNumsPos[b.sideToMove, promotionPiece, endIndex];
		}

		/* Enforcing Castling */
		if (startPieceN == 0 && (startIndex % 8) == 4)
		{
			for (int sideN = 0; sideN < 2; sideN++)
			{
				if (endIndex == g.castlingKingPos[b.sideToMove, sideN]) // if king went queenside or kingside
				{
					int rookFrom = g.castlingRookPosFrom[b.sideToMove, sideN];
					int rookTo = g.castlingRookPosTo[b.sideToMove, sideN];
					b.pieces[b.sideToMove, 4] &= ~(1UL << rookFrom); // removes rook from original position
					b.pieces[b.sideToMove, 4] |= 1UL << rookTo; // places rook to new position
					b.zobristKey ^= g.zobristNumsPos[b.sideToMove, 4, rookFrom];
					b.zobristKey ^= g.zobristNumsPos[b.sideToMove, 4, rookTo];
				}
			}
		}

		/* Updating Castling Rights */
		if (startPieceN == 0) // if king moved
		{
			for (int sideN = 0; sideN < 2; sideN++)
			{
				if (b.castlingRights[b.sideToMove, sideN]) // only set to false when already true
				{
					b.castlingRights[b.sideToMove, sideN] = false;
					b.zobristKey ^= g.zobristNumsCastling[b.sideToMove, sideN];
				}
			}
		}

		if (startPieceN == 4 || move.capturedPiece == 4) // if rook moved or got captured
		{
			for (int sideN = 0; sideN < 2; sideN++)
			{
				if (startIndex == g.rookPos[b.sideToMove, sideN] && // if own's rook moved
					b.castlingRights[b.sideToMove, sideN]) // only set to false when already true
				{
					b.castlingRights[b.sideToMove, sideN] = false;
					b.zobristKey ^= g.zobristNumsCastling[b.sideToMove, sideN];
				}
				if (endIndex == g.rookPos[1 - b.sideToMove, sideN] && // if enemy's rook got captured
					b.castlingRights[1 - b.sideToMove, sideN]) // only set to false when already true
				{
					b.castlingRights[1 - b.sideToMove, sideN] = false;
					b.zobristKey ^= g.zobristNumsCastling[1 - b.sideToMove, sideN];
				}
			}
		}
		
		
		/* Enforcing En Passant */
		if (move.isEnPassant)
		{
			int targetOfEnPassant = endIndex + g.SinglePush(1 - b.sideToMove);
			b.pieces[1 - b.sideToMove, 5] &= ~(1UL << targetOfEnPassant); // removes en passant target enemy pawn from board
			b.zobristKey ^= g.zobristNumsPos[1 - b.sideToMove, 5, targetOfEnPassant];
		}

		/* Updating En Passant */
		if (b.enPassantSquare != -1)
			b.zobristKey ^= g.zobristNumsEnPassant[b.enPassantSquare % 8];

		if (startPieceN == 5 && // if pawn moved
			((startIndex / 8) == g.DoubleStartRank(b.sideToMove) && 
			 (endIndex / 8) == g.DoubleEndRank(b.sideToMove))) // if pawn double moved
		{
			b.enPassantSquare = endIndex - (isWhitesTurn ? 8 : -8);
		}
		else
		{
			b.enPassantSquare = -1;
		}

		if (b.enPassantSquare != -1)
			b.zobristKey ^= g.zobristNumsEnPassant[b.enPassantSquare % 8];

		
		/* Updating Clocks */
		if (move.capturedPiece != -1 || startPieceN == 5)
		{
			b.halfMoveClock = 0;
		}
		else
		{
			b.halfMoveClock++;
		}
		
		if (!isWhitesTurn)
		{
			b.fullMoveCounter++;
		}

		/* Updating Side To Move */
		b.sideToMove = 1 - b.sideToMove;
		b.lastMove = move;
		b.zobristKey ^= g.zobristNumSideToMove;

		/* Adding Zobrist Key to History */
		b.zobristHistory.Add(b.zobristKey);

		/* Updating Variables */
		Update();

		// GD.Print(String.Format("Castling Rights: {4}\nSide to move: {0}\nEn passant: {1}\nHalfmove: {2}\nFullmove: {3}\n", b.sideToMove, b.enPassantSquare, b.halfMoveClock, b.fullMoveCounter, b.castlingRights[0, 0]));
	}

	public void UnmakeMove(Move move, ref Board bPrev)
	{
		int startPieceN = move.pieceN;
		int startIndex = move.start;
		int endIndex = move.end;
		int promotionPiece = move.promotionPiece;

		/* Reversing Side To Move */
		b.sideToMove = 1 - b.sideToMove;

		/* Reversing Castling */
		if (startPieceN == 0 && (startIndex % 8) == 4)
		{
			for (int sideN = 0; sideN < 2; sideN++)
			{
				if (endIndex == g.castlingKingPos[b.sideToMove, sideN])
				{
					b.pieces[b.sideToMove, 4] |= 1UL << g.castlingRookPosFrom[b.sideToMove, sideN];
					b.pieces[b.sideToMove, 4] &= ~(1UL << g.castlingRookPosTo[b.sideToMove, sideN]);
				}
			}
		}

		/* Reversing Promotion */
		if (g.IsPromotion(b.sideToMove, startPieceN, endIndex))
		{
			b.pieces[b.sideToMove, promotionPiece] &= ~(1UL << endIndex);
			b.pieces[b.sideToMove, 5] |= 1UL << endIndex;
		}

		/* Reversing Move */
		b.pieces[b.sideToMove, startPieceN] &= ~(1UL << endIndex);
		b.pieces[b.sideToMove, startPieceN] |= 1UL << startIndex;

		/* Reversing Capture */
		if (move.capturedPiece != -1)
		{
			if (endIndex == bPrev.enPassantSquare && startPieceN == 5) // if move is en passant
			{
				int targetOfEnPassant = endIndex + g.SinglePush(1 - b.sideToMove);
				b.pieces[1 - b.sideToMove, 5] |= 1UL << targetOfEnPassant;
			}
			else
			{
				b.pieces[1 - b.sideToMove, move.capturedPiece] |= 1UL << endIndex;
			}
		}

		/* Reversing Other Variables */
		b = bPrev.Clone();
	}

	public void GeneratePossibleMoves(){
		b.kingPos = g.BitScan(b.pieces[b.sideToMove, 0]);
		b.isInCheck = IsKingInCheck(b.kingPos, b.sideToMove);

		if (b.isInCheck)
			b.checkingPieces = GetCheckingPieces(b.kingPos, b.sideToMove);
		else
			b.checkingPieces = 0UL;
		
		b.pinnedMobility = new Dictionary<int, ulong> {};
		b.pinnedPieces = GetPinnedPieces();

		for (int pieceN = 0; pieceN < 6; pieceN++)
		{
			b.possibleMovesBB[pieceN] = new Dictionary<int, ulong> {};

			foreach (int pieceIndex in b.piecesSer[b.sideToMove, pieceN])
			{
				ulong pieceMoves = GenerateMovesByIndex(pieceN, pieceIndex, b.sideToMove);
				b.possibleMovesBB[pieceN].Add(pieceIndex, pieceMoves);
			}

			// /* Enemy Attacks Bitboard */
			// foreach (int pieceIndex in b.piecesSer[1 - b.sideToMove, pieceN])
			// {
			// 	ulong pieceMoves = GenerateMovesByIndex(pieceN, pieceIndex, 1 - b.sideToMove);
			// 	enemyAttacks |= pieceMoves;
			// }
		}
	}

	public void flattenPossibleMoves()
	{
		b.possibleMoves = new List<Move> {};

		for (int pieceN = 0; pieceN < 6; pieceN++)
		{
			Dictionary<int, ulong> pieceType = b.possibleMovesBB[pieceN];
			foreach (var piece in pieceType.Keys.ToList())
			{
				foreach (var endIndex in g.Serialize(pieceType[piece]))
				{
					int capturedPiece = FindPieceN(endIndex); // -1 if not capture, pieceN if capture
					int[] promotionPieces = g.IsPromotion(b.sideToMove, pieceN, endIndex) ?
									g.promotionPieces : // if move is promoting
									new int[] {-1}; // if move is not promoting

					foreach (int promotionPiece in promotionPieces)
					{
						Move move = new Move(pieceN, piece, endIndex, promotionPiece, capturedPiece);

						if (pieceN == 5 && endIndex == b.enPassantSquare) // if en passant capture
						{
							capturedPiece = 5;
							move.isEnPassant = true;
						}

						b.possibleMoves.Add(move);
					}
				}
			}
		}
	}

	public ulong GenerateMovesByIndex(int pieceN, int index, int colorN){

		/* GENERATING (MOSTLY) PSEUDO-LEGAL MOVES */
		ulong pseudoLegalMoves = 0;

		switch (pieceN)
		{
			// KING
			case 0:
				pseudoLegalMoves |= g.kingAttacks[index];
				pseudoLegalMoves &= ~b.occupancyByColor[colorN];

				/* Detecting Castling */
				for (int sideN = 0; sideN < 2; sideN++)
				{
					ulong path = g.castlingMasks[colorN, sideN];
					bool hasCastlingRight = b.castlingRights[colorN, sideN];
					bool isPathClear = (b.occupancy & path) == 0;

					if (!b.isInCheck && hasCastlingRight && isPathClear)
					{
						bool isPathSafe = true;
						path &= ~(0x200000000000002UL); // fixing long castling bug by excluding b1 and b8
						
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
				pseudoLegalMoves &= ~b.occupancyByColor[colorN];
				break;
			
			// BISHOP
			case 2:
				pseudoLegalMoves |= GenerateBishopAttacks(index);
				pseudoLegalMoves &= ~b.occupancyByColor[colorN];
				break;
			
			// KNIGHT
			case 3:
				pseudoLegalMoves |= g.knightAttacks[index];
				pseudoLegalMoves &= ~b.occupancyByColor[colorN];
				break;
			
			// ROOK
			case 4:
				pseudoLegalMoves |= GenerateRookAttacks(index);
				pseudoLegalMoves &= ~b.occupancyByColor[colorN];
				break;
			
			// PAWN
			case 5:
				pseudoLegalMoves |= g.pawnMoves[colorN, index] & ~b.occupancy; // single push

				if ((index / 8) == g.DoubleStartRank(colorN) // if pawn is in home rank
					&& pseudoLegalMoves != 0) // if pawn move is not blocked in the single push
				{
					pseudoLegalMoves |= 1UL << (index + 2 * g.SinglePush(colorN)) & ~b.occupancy; // double push
				}

				pseudoLegalMoves |= g.pawnAttacks[colorN, index] & b.occupancyByColor[1 - colorN];

				/* Detecting En Passant */
				bool isEnPassantAllowed = (g.pawnAttacks[colorN, index] >> b.enPassantSquare & 1UL) == 1;
				if (isEnPassantAllowed){				
					int enPassantTarget = b.enPassantSquare + g.SinglePush(1 - b.sideToMove);
					ulong hiddenPawns = b.occupancy & ((1UL << enPassantTarget) | (1UL << index));  // "remove" en passant target and en passanter from board
					ulong enemiesAfterEnPassant = GenerateRookAttacks(b.kingPos, hiddenPawns);

					enemiesAfterEnPassant &= b.pieces[1 - b.sideToMove, 4] |
											 b.pieces[1 - b.sideToMove, 1];
					enemiesAfterEnPassant &= ~b.occupancyByColor[b.sideToMove];
					
					if (enemiesAfterEnPassant == 0)
					{
						pseudoLegalMoves |= 1UL << b.enPassantSquare;
					}
				}

				break;
			
		}

		/* GENERATING LEGAL MOVES */
		ulong legalMoves = pseudoLegalMoves;

		/* Absolutely Pinned Pieces */
		if ((b.pinnedPieces >> index & 1UL) == 1) // if piece is pinned
		{
			legalMoves &= b.pinnedMobility[index];
		}

		/* Capturing and Blocking Checking Pieces */
		if (b.isInCheck & !(pieceN == 0)) // not including king since its moves have already been filtered
		{
			if (g.Serialize(b.checkingPieces).Count == 1)
			{
				ulong capturingMoves = legalMoves & b.checkingPieces;
				
				if (pieceN == 5 && 										// if pawn
					(legalMoves >> b.enPassantSquare & 1UL) == 1)		// and en passant is pseudolegal
				{
					int enPassantTarget = b.enPassantSquare + g.SinglePush(1 - b.sideToMove);

					if ((b.checkingPieces >> enPassantTarget & 1UL) == 1) // if enPassantTarget is a checking piece
					{
						capturingMoves |= 1UL << b.enPassantSquare; // include en passant target in capturing moves
					}
				}

				int checkingPieceIndex = g.BitScan(b.checkingPieces);
				ulong checkingPath = g.inBetween[b.kingPos, checkingPieceIndex];
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

	public ulong GenerateRookAttacks(int index, ulong hidden = 0UL)
	{
		ulong output = 0UL;

		for (int dir = 1; dir < 8; dir += 2)
		{
			output |= GetBlockedRayAttack(index, dir, hidden);
		}

		return output;
	}

	public ulong GenerateBishopAttacks(int index, ulong hidden = 0UL)
	{
		ulong output = 0UL;

		for (int dir = 0; dir < 8; dir += 2)
		{
			output |= GetBlockedRayAttack(index, dir, hidden);
		}

		return output;
	}

	public ulong GetBlockedRayAttack(int index, int dir, ulong hidden = 0UL)
	{
		ulong attacks = g.rayAttacks[index, dir];
		ulong blockers = attacks & b.occupancy;

		if (hidden != 0UL)
			blockers &= ~hidden; // removing king from blockers to avoid check problems

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
		ulong kingUL = 1UL << b.kingPos;

		ulong enemyPawns = b.pieces[1 - colorN, 5];
		ulong kingAsPawn = g.pawnAttacks[colorN, kingIndex];
		if ((enemyPawns & kingAsPawn) != 0UL) {return true;}

		ulong enemyKnights = b.pieces[1 - colorN, 3];
		ulong kingAsKnight = g.knightAttacks[kingIndex];
		if ((enemyKnights & kingAsKnight) != 0UL)  {return true;}

		ulong enemyRQ = b.pieces[1 - colorN, 4] | 
						b.pieces[1 - colorN, 1];
		ulong kingAsRook = GenerateRookAttacks(kingIndex, kingUL);
		if ((enemyRQ & kingAsRook) != 0UL)  {return true;}

		ulong enemyBQ = b.pieces[1 - colorN, 2] | 
						b.pieces[1 - colorN, 1];
		ulong kingAsBishop = GenerateBishopAttacks(kingIndex, kingUL);
		if ((enemyBQ & kingAsBishop) != 0UL)  {return true;}

		ulong enemyKing = b.pieces[1 - colorN, 0];
		ulong kingAsKing = g.kingAttacks[kingIndex];
		if ((enemyKing & kingAsKing) != 0UL)  {return true;}

		return false;
	}
	
	public ulong GetCheckingPieces(int kingIndex, int colorN)
	{
		ulong kingUL = 1UL << b.kingPos;

		ulong enemyPawns = b.pieces[1 - colorN, 5];
		ulong kingAsPawn = g.pawnAttacks[colorN, kingIndex];

		ulong enemyKnights = b.pieces[1 - colorN, 3];
		ulong kingAsKnight = g.knightAttacks[kingIndex];

		ulong enemyRQ = b.pieces[1 - colorN, 4] | 
						b.pieces[1 - colorN, 1];
		ulong kingAsRook = GenerateRookAttacks(kingIndex, kingUL);

		ulong enemyBQ = b.pieces[1 - colorN, 2] | 
						b.pieces[1 - colorN, 1];
		ulong kingAsBishop = GenerateBishopAttacks(kingIndex, kingUL);

		return (enemyPawns & kingAsPawn) |
			   (enemyKnights & kingAsKnight) |
			   (enemyRQ & kingAsRook) |
			   (enemyBQ & kingAsBishop);
	}

	public ulong GetPinnedPieces()
	{
		ulong pinned = 0UL;
		ulong pinners = GenerateRookXRay(b.occupancyByColor[b.sideToMove], // own pieces as nearest blocker
										 b.occupancy, // all pieces as second nearest blocker
										 b.kingPos); // king position
		pinners &= b.pieces[1 - b.sideToMove, 4] | // get opponent's rooks
				   b.pieces[1 - b.sideToMove, 1];  // get opponent's queens
		pinners &= ~b.occupancyByColor[b.sideToMove];

		foreach (int pinner in g.Serialize(pinners))
		{
			ulong tPinned = g.inBetween[b.kingPos, pinner] & b.occupancyByColor[b.sideToMove]; // own pieces between king and pinner
			pinned |= tPinned;

			foreach (int pinnedIndex in g.Serialize(tPinned))
			{
				if (!b.pinnedMobility.ContainsKey(pinnedIndex))
				{
					b.pinnedMobility.Add(pinnedIndex, g.inBetween[b.kingPos, pinner]);
				}
			}
		}


		pinners = GenerateBishopXRay(b.occupancyByColor[b.sideToMove],
										 b.occupancy,
										 b.kingPos);
		pinners &= b.pieces[1 - b.sideToMove, 2] | // get opponent's bishops
				   b.pieces[1 - b.sideToMove, 1];  // get opponent's queens
		pinners &= ~b.occupancyByColor[b.sideToMove];

		foreach (int pinner in g.Serialize(pinners))
		{
			ulong tPinned = g.inBetween[b.kingPos, pinner] & b.occupancyByColor[b.sideToMove];
			pinned |= tPinned;

			foreach (int pinnedIndex in g.Serialize(tPinned))
			{
				if (!b.pinnedMobility.ContainsKey(pinnedIndex))
				{
					b.pinnedMobility.Add(pinnedIndex, g.inBetween[b.kingPos, pinner]);
				}
			}
		}

		return pinned;
	}

	public ulong GetZobristKey()
	{
		ulong res = 0;

		if (b.enPassantSquare != -1)
		{
			res ^= g.zobristNumsEnPassant[b.enPassantSquare % 8];
		}

		if (b.sideToMove == 1)
		{
			res ^= g.zobristNumSideToMove;
		}

		for (int colorN = 0; colorN < 2; colorN++)
		{
			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				foreach (int pieceIndex in b.piecesSer[colorN, pieceN])
				{
					res ^= g.zobristNumsPos[colorN, pieceN, pieceIndex];
				}
			}

			for (int sideN = 0; sideN < 2; sideN++)
			{
				if (b.castlingRights[colorN, sideN])
				{
					res ^= g.zobristNumsCastling[colorN, sideN];
				}
			}
		}

		return res;
	}

	public int Evaluate(bool debug = false) // REMINDER: Evaluate() is from an objective point of view of the two players, b.sideToMove cannot be used here
	{
		/* Draw */
		if (b.gameOutcome == 2)
		{
			return 0;
		}

		/* Checkmate */
		if (b.gameOutcome != -1)
		{
			return g.sign[b.gameOutcome] * 32000;
		}

		
		int materialValue = 0;
		int mobilityScore = 0;
		int placementScore = 0;
		int[] kingTropism = new int[2] {0, 0};
		
		for (int colorN = 0; colorN < 2; colorN++)
		{
			int sign = g.sign[colorN];
			int enemyKingPos = g.BitScan(b.pieces[1 - colorN, 0]);

			bool isOpponent = (colorN != b.sideToMove);
			if (isOpponent)
			{
				b.sideToMove = 1 - b.sideToMove;
				Update();
			}


			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				/* Material Value & King Tropism */
				int pieceBonus = 0;
				int pieceCount = b.piecesCount[colorN, pieceN];
				materialValue += sign * g.piecesValue[pieceN] * pieceCount;

				switch (pieceN)
				{
					/* KING */
					case 0:
						for (int fileDir = -1; fileDir <= 1; fileDir++)
						{
							int curFile = (b.kingPos % 8) + fileDir;

							if (curFile >= 0 && curFile < 8)
							{
								for (int pawnColor = 0; pawnColor < 2; pawnColor++)
								{
									if (!b.pawnFileSet[pawnColor, curFile])
									{
										pieceBonus -= (fileDir == 0) ? 30 : 15; // penalty when king's file or adjacent files are semi-open/open
									}
								}
							}
						}

						if (!b.isEndgame)
						{
							for (int sideN = 0; sideN < 2; sideN++)
							{
								if (b.kingPos == g.castlingKingPos[colorN, sideN])
								{
									ulong pawnShield = b.pieces[colorN, 5] & g.pawnShieldMask[colorN, sideN];
									pieceBonus += 20 * g.Serialize(pawnShield).Count; // bonus for each pawn in pawn shield
								}
							}
						}

						break;
					
					/* QUEEN */
					case 1:
						foreach (int queenIndex in b.piecesSer[colorN, pieceN])
						{
							kingTropism[1 - colorN] -= g.distanceBonus[queenIndex, enemyKingPos] * 5 / 2;
						}
						break;

					/* BISHOP */
					case 2:
						if (b.piecesCount[colorN, 2] == 2)
						{
							pieceBonus += 50; // bishop pair bonus
						}

						foreach (int bishopIndex in b.piecesSer[colorN, pieceN])
						{
							kingTropism[1 - colorN] -= g.distanceBonus[bishopIndex, enemyKingPos] / 2;
						}

						break;
					
					/* KNIGHT */
					case 3:
						foreach (int knightIndex in b.piecesSer[colorN, pieceN])
						{
							pieceBonus -= 4 * (8 - b.piecesCount[colorN, 5]); // decreasing knight value as pawns decrease
							kingTropism[1 - colorN] -= g.distanceBonus[knightIndex, enemyKingPos];
						}
						break;

					/* ROOK */
					case 4:
						foreach (int rookIndex in b.piecesSer[colorN, pieceN])
						{
							pieceBonus += 7 * (8 - b.piecesCount[colorN, 5]); // increasing rook value as pawns decrease
							
							if (!b.pawnFileSet[colorN, rookIndex % 8])
							{
								pieceBonus += 20; // increasing rook value when in a semi-open/open file
							}
						}
						
						break;
					
					/* PAWN */
					case 5:
						ulong pawnMap = b.pieces[colorN, pieceN];
						ulong enemyPawnMap = b.pieces[1 - colorN, pieceN];
						foreach (int pawnIndex in b.piecesSer[colorN, pieceN])
						{
							ulong blockingPawns = pawnMap & g.frontSpan[colorN, pawnIndex];
							pieceBonus -= 35 * g.Serialize(blockingPawns).Count; // decreases pawn value if blocked (doubled, tripled, etc.)

							ulong passedPawnObstacles = enemyPawnMap & g.passedFrontSpan[colorN, pawnIndex];
							if (passedPawnObstacles == 0UL)
							{
								int distanceFromHome = Math.Abs((pawnIndex / 8) - g.DoubleStartRank(colorN)) + 1;
								pieceBonus += 30 * distanceFromHome; // increases passed pawn value as rank increases
							}

							ulong friendlyNeighborPawns = pawnMap & g.neighborFiles[pawnIndex % 8];
							if (friendlyNeighborPawns == 0UL)
							{
								pieceBonus -= 20; // decreases pawn value if isolated
							}
						}
						
						break;
				}

				materialValue += sign * pieceBonus;

				/* Placement Score */
				foreach (int index in b.piecesSer[colorN, pieceN])
				{
					int file = index % 8;
					int rank = (colorN == 1) ? (index / 8) : (7 - index / 8); // flip rank if white
					int sq =  g.ToIndex(file, rank);
					
					if (b.isEndgame && pieceN == 0)
					{
						placementScore += sign * g.kingEndgamePSTable[sq];
					}
					else
					{
						placementScore += sign * g.pieceSquareTables[pieceN, sq];
					}
				}
			}

			/* Mobility */
			mobilityScore += sign * CalculateMobility(colorN);


			if (isOpponent)
			{
				b.sideToMove = 1 - b.sideToMove;
				Update();
			}
		}

		return materialValue + 
			   2 * mobilityScore + 
			   placementScore +
			   2 * (kingTropism[0] - kingTropism[1]);
	}

	public int CalculateMobility(int colorN)
	{
		int mobilityScore = 0;

		foreach (Move move in b.possibleMoves)
		{
			mobilityScore += 1;
		}

		return mobilityScore;
	}
}