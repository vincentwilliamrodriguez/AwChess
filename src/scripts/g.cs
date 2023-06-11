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
	public static ulong curPossibleMoves = 0;

	public static ulong[,] rayAttacks = new ulong[64,8]; // dimensions are squares and directions (starting from NW clockwise)
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

	public static void InitRayAttacks() {
		for (int from = 0; from < 64; from++)
		{
			for (int dir = 0; dir < 8; dir++)
			{
				rayAttacks[from, dir] = 0UL;
				int to = from + dirNums[dir];
				int toPrev = from;

				while (IsWithinBoard(to))
				{
					int toRank = to % 8;
					int toFile = to / 8;
					bool sameFile = (toPrev % 8) == toRank;
					bool sameRank = (toPrev / 8) == toFile;
					bool farFile = Math.Abs((toPrev % 8) - toRank) == 2;
					bool farRank = Math.Abs((toPrev / 8) - toFile) == 2;

					bool allowed = (dir % 2 == 0) ? !(sameRank || sameFile || farFile || farRank) : // nw, ne, se, sw
								   ((dir % 4 == 3) ? sameRank : // e, w
								   true); // n, s

					if (!allowed)
					{
						break;
					}
					
					rayAttacks[from, dir] |= 1UL << to;

					toPrev = to;
					to += dirNums[dir];
				}
			}
		}
	}
}
