using Godot;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public static partial class g : Object
{
	public static bool[] isPlayer = new bool[] {true, true};
	public static int botSpeed = 200;
	public static int botDepth = 4;
	public static int botMaxID = 2000;

	public static bool isBoardFlipped = isPlayer[1] && !isPlayer[0]; // only flip when black is player but not both
	public static bool isMovingPiece = false;
	public static int selectedPiece = -1; // [piece, index]
	public static int selectedPieceN = -1;
	public static ulong curHighlightedMoves = 0UL;
	public static string startingPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
	public static char[] piecesArray = new char[] {'k', 'q', 'b', 'n', 'r', 'p'};
	public static string[] piecesMoveArray = new string[] {"K", "Q", "B", "N", "R", ""};
	public static char[] fileArray = new char[] {'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'};

	public static ulong[,] rayAttacks = new ulong[64,8]; // dimensions are square and direction (starting from NW clockwise)
	public static ulong[] kingAttacks = new ulong[64]; // dimension is square
	public static ulong[] knightAttacks = new ulong[64]; // dimension is square
	public static ulong[,] pawnMoves = new ulong[2, 64]; // dimensions are color and square
	public static ulong[,] pawnAttacks = new ulong[2, 64]; // dimensions are color and square
	public static ulong[,] frontSpan = new ulong[2, 64]; // dimensions are color and square
	public static ulong[,] passedFrontSpan = new ulong[2, 64]; // dimensions are color and square
	public static ulong[] neighborFiles = new ulong[8]; // dimension is file
	public static int[,] distanceBonus = new int[64, 64]; // dimensions are from and to squares

	public static ulong[,] castlingMasks = new ulong[2, 2] {{0xE, 0x60}, 
															{0xE00000000000000, 0x6000000000000000}};
	public static ulong[,] pawnShieldMask = new ulong[2, 2] {{0x70700, 0xE0E000},
														   {0x7070000000000, 0xE0E00000000000}};
	public static int[] castlingKingPosFrom = new int[2] {4, 60};
	public static int[,] castlingKingPos = new int[2, 2] {{2, 6}, {58, 62}};
	public static int[,] castlingRookPosFrom = new int[2, 2] {{0, 7}, {56, 63}};
	public static int[,] castlingRookPosTo = new int[2, 2] {{3, 5}, {59, 61}};
	public static int[,] rookPos = new int[2, 2] {{0, 7}, {56, 63}};

	public static int[] promotionRank = new int[2] {7, 0};
	public static int[] promotionPieces = new int[4] {1, 2, 3, 4};
	public static int promotionTarget = -1;
	public static bool isPromoting = false;
	public static ulong[,] inBetween = new ulong[64, 64];

	public static int perftSpeed = 0;
	public static int perftDepth = 5;
	public static ulong testHighlight = 0UL;
	public static string debugLabel = "";
	
	public static int[] sign = new int[2] {1, -1};
	public static int[] piecesValue = {0, 900, 330, 320, 500, 100};
	public static int staticEvaluation = 0;
	public static int positiveInfinity = 10000000;
	public static int negativeInfinity = -10000000;

	public static Random random = new Random(31415);
	public static ulong[,,] zobristNumsPos = new ulong[2, 6, 64];
	public static ulong zobristNumSideToMove; // only applied when sideToMove is 1 (black)
	public static ulong[,] zobristNumsCastling = new ulong[2, 2];
	public static ulong[] zobristNumsEnPassant = new ulong[8];
	public static Dictionary<ulong, Dictionary<string, MoveFreq>> openingBook;

	public static double moveSpeed = 0.1;
	public static ulong[,] piecesDisplay = new ulong[2, 6];
	public static Node mainNode;

	public static Dictionary<int, int> dirNums = new Dictionary<int, int> 
	{
		{0, 7}, // North West
		{1, 8}, // North
		{2, 9}, // North East
		{3, 1}, // East
		{4, -7}, // South East
		{5, -8}, // South
		{6, -9}, // South West
		{7, -1} // West
	};
	public static Dictionary<int, int> dirNumsKnight = new Dictionary<int, int> 
	{
		{0, 6}, // North West West
		{1, 15}, // North Nort West
		{2, 17}, // North North East
		{3, 10}, // North East East
		{4, -6}, // South East East
		{5, -15}, // South South East
		{6, -17}, // South South West
		{7, -10} // South West West
	};

	public static int[,] pieceSquareTables = new int[6, 64] {
		/* KING */
		{-30,-40,-40,-50,-50,-40,-40,-30,
		-30,-40,-40,-50,-50,-40,-40,-30,
		-30,-40,-40,-50,-50,-40,-40,-30,
		-30,-40,-40,-50,-50,-40,-40,-30,
		-20,-30,-30,-40,-40,-30,-30,-20,
		-10,-20,-20,-20,-20,-20,-20,-10,
		20, 20,  0,  0,  0,  0, 20, 20,
		20, 40, 0,  0,  0, 0, 40, 20},
		
		/* QUEEN */
		{-20,-10,-10, -5, -5,-10,-10,-20,
		-10,  0,  0,  0,  0,  0,  0,-10,
		-10,  0,  5,  5,  5,  5,  0,-10,
		-5,  0,  5,  5,  5,  5,  0, -5,
		0,  0,  5,  5,  5,  5,  0, -5,
		-10,  5,  5,  5,  5,  5,  0,-10,
		-10,  0,  5,  0,  0,  0,  0,-10,
		-20,-10,-10, -5, -5,-10,-10,-20}, 
		
		/* BISHOP */
		{-20,-10,-10,-10,-10,-10,-10,-20,
		-10,  0,  0,  0,  0,  0,  0,-10,
		-10,  0,  5, 10, 10,  5,  0,-10,
		-10,  5,  5, 10, 10,  5,  5,-10,
		-10,  0, 10, 10, 10, 10,  0,-10,
		-10, 10, 10, 10, 10, 10, 10,-10,
		-10,  5,  0,  0,  0,  0,  5,-10,
		-20,-10,-10,-10,-10,-10,-10,-20,}, 
		
		/* KNIGHT */
		{-50,-40,-30,-30,-30,-30,-40,-50,
		-40,-20,  0,  0,  0,  0,-20,-40,
		-30,  0, 10, 15, 15, 10,  0,-30,
		-30,  5, 15, 20, 20, 15,  5,-30,
		-30,  0, 15, 20, 20, 15,  0,-30,
		-30,  5, 10, 15, 15, 10,  5,-30,
		-40,-20,  0,  5,  5,  0,-20,-40,
		-50,-40,-30,-30,-30,-30,-40,-50,}, 
		
		/* ROOK */
		{  0,  0,  0,  0,  0,  0,  0,  0,
		5, 10, 10, 10, 10, 10, 10,  5,
		-5,  0,  0,  0,  0,  0,  0, -5,
		-5,  0,  0,  0,  0,  0,  0, -5,
		-5,  0,  0,  0,  0,  0,  0, -5,
		-5,  0,  0,  0,  0,  0,  0, -5,
		-5,  0,  0,  0,  0,  0,  0, -5,
		0,  0,  0,  5,  5,  0,  0,  0},

		/* PAWN */
		{0,  0,  0,  0,  0,  0,  0,  0,
		50, 50, 50, 50, 50, 50, 50, 50,
		10, 10, 20, 30, 30, 20, 10, 10,
		5,  5, 10, 25, 25, 10,  5,  5,
		0,  0,  0, 20, 20,  0,  0,  0,
		5, -5,-10,  0,  0,-10, -5,  5,
		5, 10, 10,-20,-20, 10, 10,  5,
		0,  0,  0,  0,  0,  0,  0,  0}
	};

	public static int[] kingEndgamePSTable = new int[64]
	{
		-50,-40,-30,-20,-20,-30,-40,-50,
		-30,-20,-10,  0,  0,-10,-20,-30,
		-30,-10, 20, 30, 30, 20,-10,-30,
		-30,-10, 30, 40, 40, 30,-10,-30,
		-30,-10, 30, 40, 40, 30,-10,-30,
		-30,-10, 20, 30, 30, 20,-10,-30,
		-30,-30,  0,  0,  0,  0,-30,-30,
		-50,-30,-30,-30,-30,-30,-30,-50
	};

	
	public static void Init() {
		for (int from = 0; from < 64; from++)
		{
			/* Ray Attacks */
			for (int dir = 0; dir < 8; dir++)
			{
				rayAttacks[from, dir] = 0UL;
				int to = from + dirNums[dir];
				int toKnight = from + dirNumsKnight[dir];
				int toPrev = from;

				/* King Attacks */
				if (WrapCheck(from, dirNums[dir]))
					kingAttacks[from] |= 1UL << to;

				/* Knight Attacks */
				if (WrapCheck(from, dirNumsKnight[dir]))
					knightAttacks[from] |= 1UL << toKnight;
				
				/* Sliding Attacks */
				while (WrapCheck(toPrev, dirNums[dir]))
				{
					rayAttacks[from, dir] |= 1UL << to;

					toPrev = to;
					to += dirNums[dir];
				}
			}


			/* Pawn Moves and Attacks */
			for (int colorN = 0; colorN < 2; colorN++)
			{
				/* Pawn Moves */
				int pawnFile = from % 8;
				int pawnRank = from / 8;
				pawnMoves[colorN, from] = 0UL | (1UL << (from + SinglePush(colorN)));

				/* Pawn Attacks */
				pawnAttacks[colorN, from] = 0UL;

				if (pawnFile != 0) // if not on the A file
				{
					pawnAttacks[colorN, from] |= 1UL << (from + SinglePush(colorN) - 1);
				}

				if (pawnFile != 7) // if not on the H file
				{
					pawnAttacks[colorN, from] |= 1UL << (from + SinglePush(colorN) + 1);
				}

				/* Squares Ahead of a Pawn */
				frontSpan[colorN, from] = rayAttacks[from, (colorN == 0) ? 1 : 5];
			}
		}
		
		/* Squares Ahead of a Passed Pawn */
		for (int from = 0; from < 64; from++)
		{
			for (int colorN = 0; colorN < 2; colorN++)
			{
				passedFrontSpan[colorN, from] = frontSpan[colorN, from];

				foreach (int dir in new int[] {3, 7}) // side direction
				{
					if (WrapCheck(from, dirNums[dir]))
					{
						passedFrontSpan[colorN, from] |= frontSpan[colorN, from + dirNums[dir]];
					}
				}
			}
		}

		/* Neighbor Files */
		for (int file = 0; file < 8; file++)
		{
			neighborFiles[file] = 0UL;

			foreach (int dir in new int[] {3, 7})
			{
				if (WrapCheck(file, dirNums[dir]))
				{
					neighborFiles[file] |= (ulong) (0x101010101010101 << (file + dirNums[dir]));
				}
			}

		}

		/* In-between Lookup & Distance Bonus */
		for (int from = 0; from < 64; from++)
		{
			for (int to = 0; to < 64; to++)
			{
				inBetween[from, to] = 0UL;
				for (int dir = 0; dir < 8; dir++)
				{
					ulong fromRay = rayAttacks[from, dir];
					if (((fromRay >> to) & 1) == 1) // if 'to' is reached by 'fromRay'
					{
						inBetween[from, to] = fromRay ^ rayAttacks[to, dir];
					}
				}

				distanceBonus[from, to] = 14 - (Math.Abs(from % 8 - to % 8) + Math.Abs(from / 8 - to / 8));
			}
		}

		/* Zobrist Numbers Initialization */

		for (int fileN = 0; fileN < 8; fileN++)
		{
			zobristNumsEnPassant[fileN] = RandomULong();
		}

		zobristNumSideToMove = RandomULong();

		for (int colorN = 0; colorN < 2; colorN++)
		{
			for (int pieceN = 0; pieceN < 6; pieceN++)
			{
				for (int i = 0; i < 64; i++)
				{
					zobristNumsPos[colorN, pieceN, i] = RandomULong();
				}
			}

			for (int sideN = 0; sideN < 2; sideN++)
			{
				zobristNumsCastling[colorN, sideN] = RandomULong();
			}
		}

		/* Opening Book Initialization */
		// using var openingBookFile = FileAccess.Open("user://opening_book.json", FileAccess.ModeFlags.Read);
		// g.openingBook = JsonSerializer.Deserialize<Dictionary<ulong, Dictionary<string, int>>>(openingBookFile.GetAsText());

		// GD.Print("AwAw ", g.openingBook.Count);
	}

	public static void PrintBitboard(ulong[,] inp) {
		foreach (var piece in inp) {
			GD.Print(piece);
		}
	}

	public static int ToIndex(int file, int rank) {
		return 8 * rank + file;
	}

	public static bool IsWithinBoard(int file, int rank) {
		return file >= 0 && file < 8 && rank >= 0 && rank < 8;
	}

	public static bool IsWithinBoard(int index) {
		return index >= 0 && index < 64;
	}

	public static int BitScan(ulong val, bool isPositive = true) {
		if (val == 0) {return -1;}
		return isPositive ? BitOperations.TrailingZeroCount(val) : // LS1B
					    	63 - BitOperations.LeadingZeroCount(val); // MS1B
	}

	public static List<int> Serialize(ulong x) {
		List<int> output = new List<int> {};

		while (x != 0UL)
		{
			output.Add(g.BitScan(x)); // Adds index of LSB to serialized list
			x &= x - 1;
		}

		return output;
	}

	public static int DoubleStartRank(int color) {
		return (color == 0) ? 1 : 6;
	}

	public static int DoubleEndRank(int color) {
		return (color == 0) ? 3 : 4;
	}

	public static int SinglePush(int color) {
		return (color == 0) ? 8 : -8;
	}

	public static int CanFlip(int n) {
		return isBoardFlipped ? 7-n : n;
	}

	/* NOTE: MoveToString() MUST BE CALLED BEFORE MakeMove() */
	public static string MoveToString(Move move, List<Move> legalMoves)
	{
		if (move.pieceN == -1) return "N/A";
		
		/* Castling notation */
		if (move.pieceN == 0 && (move.start % 8) == 4)
		{
			if ((move.end % 8) == 2) // if king castled queenside
				return "O-O-O";

			if ((move.end % 8) == 6) // if king castled kingside
				return "O-O";
		}


		string piece = piecesMoveArray[move.pieceN];
		string startFile = "";
		string startRank = "";
		string connector = (move.capturedPiece != -1) ? "x" : "";
		string end = fileArray[move.end % 8] + Convert.ToString(move.end / 8 + 1);
		string promotion = (move.promotionPiece != -1) ? "=" + piecesMoveArray[move.promotionPiece] : "";

		/* Ambiguity Check */
		if (move.pieceN != 5)	// if not a pawn
		{
			foreach (Move moveCheck in legalMoves)
			{
				if (moveCheck.pieceN == move.pieceN &&		// if same piece type
					moveCheck.end == move.end &&			// if can move to the same square
					moveCheck.start != move.start)			// if not the same exact piece
				{
					bool sameFile = (moveCheck.start % 8) == (move.start % 8);
					bool sameRank = (moveCheck.start / 8) == (move.start / 8);

					if (!sameFile)
					{
						startFile = Convert.ToString(fileArray[move.start % 8]); // deambiguify file
					}
					else if (!sameRank)
					{
						startRank = Convert.ToString(move.start / 8 + 1);  // deambiguify rank
					}
				}
			}
		}
		else if (move.capturedPiece != -1)
		{
			startFile = Convert.ToString(fileArray[move.start % 8]);
		}

		return piece + startFile + startRank + connector + end + promotion;
	}

	public static Move StringToMove(string move, Chess source)
	{
		Board b = source.b;
		int pieceN = -1, start = -1, end = -1, promotionPiece = -1, capturedPiece = -1;

		string initial = move[0].ToString();
		string suffix = (move.Contains('+') || move.Contains('#')) ? move[move.Length - 1].ToString() : "";	// check or checkmate: get last char									
		move = move.Substring(0, move.Length - suffix.Length);

		if (initial == "O")
		{
			int sideN = Convert.ToInt32(move == "O-O");	// if castling kingside, then 1, otherwise 0

			pieceN = 0;
			start = castlingKingPosFrom[b.sideToMove];
			end = castlingKingPos[b.sideToMove, sideN];
		}
		else
		{
			pieceN = Array.IndexOf(piecesMoveArray, initial);

			if (pieceN == -1)
			{
				pieceN = 5;	// if initial is not found in array, pieceN is a pawn
			}
			else 
			{
				move = move.Substring(1);
			}

			if (move.Contains('='))	// promotion
			{
				promotionPiece = Array.IndexOf(piecesMoveArray, move[move.Length - 1].ToString());
				move = move.Substring(0, move.Length - 2);
			}

			int endFile = Array.IndexOf(fileArray, move[move.Length - 2]);
			int endRank = move[move.Length - 1] - '1';
			end = ToIndex(endFile, endRank);
			move = move.Substring(0, move.Length - 2);

			if (move.Contains('x'))	// capture
			{
				bool isEnPassant = (pieceN == 5) && (end == source.b.enPassantSquare);
				capturedPiece = isEnPassant ? 5 : source.FindPieceN(end); // en passant only captures pawn
				move = move.Substring(0, move.Length - 1);
			}

			int ambFile = -1, ambRank = -1;

			foreach (char c in move)
			{
				int ind = Array.IndexOf(fileArray, c);

				if (ind != -1)
				{
					ambFile = ind; // ambiguity file
				}
				else
				{
					ambRank = c - '1'; // ambiguity rank
				}
			}

			foreach (Move moveCheck in b.possibleMoves)
			{
				bool sameFile = (moveCheck.start % 8) == ambFile;
				bool sameRank = (moveCheck.start / 8) == ambRank;

				if (moveCheck.pieceN == pieceN &&
					moveCheck.end == end &&
					(ambFile == -1 || sameFile) &&
					(ambRank == -1 || sameRank))
				{
					start = moveCheck.start;
					break;
				}
			}

		}


		Move res = new Move(pieceN, start, end, promotionPiece, capturedPiece);
		res.isEnPassant = pieceN == 5 && end == b.enPassantSquare;

		return res;
	}

	public static void PrintMoveList(List<Move> source, List<Move> legalMoves)
	{
		GD.Print();
		foreach (Move move in source)
		{
			GD.Print(MoveToString(move, legalMoves));
		}
	}

	public static bool IsPromotion(int colorN, int pieceN, int index)
	{
		return pieceN == 5 && (index / 8) == promotionRank[colorN];
	}

	public static bool WrapCheck(int from, int add) {
		int addFile = (add % 8);
		addFile = addFile > 2 ? addFile - 8 : // if addFile exceeds 2, then subtract 8 from modulo result
					 (addFile < -2 ? addFile + 8 : // if addFile is less than -2, then add 8 to modulo result
					  addFile); // if addFile is within normal range, then keep it
		int addRank = (int) Math.Round(add / 8.0);

		int toFile = (from % 8) + addFile;
		int toRank = (from / 8) + addRank;

		return (toFile >= 0 && toFile < 8 && toRank >= 0 && toRank < 8);
	}

	public static ulong RandomULong()
	{
		byte[] buffer = new byte[8];
		random.NextBytes(buffer);

		return BitConverter.ToUInt64(buffer, 0);
	}

	public static Vector2I IndexToVector(int i)
	{
		int x = CanFlip(i % 8);
		int y = CanFlip(7 - (i / 8));
		return new Vector2I(x, y);
	}

	public static void UpdatePiecesDisplay(Board curB)
	{
		piecesDisplay = curB.pieces.Copy();
	}

	public static void UpdatePiecesDisplay(Board curB, Move move, int colorN)
	{
		piecesDisplay = curB.pieces.Copy();

		piecesDisplay[colorN, move.pieceN] &= ~(1UL << move.end);

		/* Temporarily Restoring Captured Piece */
		if (move.capturedPiece != -1)
		{
			if (move.isEnPassant)
			{
				int targetOfEnPassant = move.end + SinglePush(1 - colorN);
				piecesDisplay[1 - colorN, 5] |= 1UL << targetOfEnPassant;
			}
			else
			{
				piecesDisplay[1 - colorN, move.capturedPiece] |= 1UL << move.end;
			}
		}

		/* Hiding Rook While Castling */
		if (move.pieceN == 0 && (move.start % 8) == 4)
		{
			for (int sideN = 0; sideN < 2; sideN++)
			{
				if (move.end == castlingKingPos[colorN, sideN])
				{
					piecesDisplay[colorN, 4] &= ~(1UL << g.castlingRookPosTo[colorN, sideN]);
				}
			}
		}
	}
}

public struct MoveFreq
{
	public Move move;
	public int freq;

	public MoveFreq(Move Move, int Freq)
	{
		move = Move;
		freq = Freq;
	}
}