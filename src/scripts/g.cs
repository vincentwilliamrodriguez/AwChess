using Godot;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public static partial class g
{
	public static bool[] isPlayer = new bool[] {true, false};
	public static bool isMovingPiece = false;
	public static int selectedPiece = -1; // [piece, index]
	public static int selectedPieceN = -1;
	public static ulong curHighlightedMoves = 0UL;

	public static ulong[,] rayAttacks = new ulong[64,8]; // dimensions are square and direction (starting from NW clockwise)
	public static ulong[] kingAttacks = new ulong[64]; // dimension is square
	public static ulong[] knightAttacks = new ulong[64]; // dimension is square
	public static ulong[,] pawnMoves = new ulong[2, 64]; // dimensions are color and square
	public static ulong[,] pawnAttacks = new ulong[2, 64]; // dimensions are color and square
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

	public static void InitAttacks() {
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
			}
		}
	}
}
