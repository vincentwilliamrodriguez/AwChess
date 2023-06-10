using Godot;
using System;

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

	public static bool IsWithinBoard(int file, int rank){
		return file >= 0 && file < 8 && rank >= 0 && rank < 8;
	}
}
