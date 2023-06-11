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

	public static int BitScanForward(ulong val) {
		if (val == 0) {return -1;}
		return BitOperations.TrailingZeroCount(val);
	}

	public static List<int> Serialize(ulong x) {
		List<int> output = new List<int> {};

		while (x != 0UL)
		{
			output.Add(g.BitScanForward(x)); // Adds index of LSB to serialized list
			x &= x - 1;
		}

		return output;
	}
}
