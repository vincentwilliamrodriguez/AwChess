using Godot;
using System;

public static partial class g
{
	public static string awaw = "Awaw pogi";

	public static void PrintBitboard(ulong[,] inp) {
		foreach (var piece in inp) {
			GD.Print(piece);
		}
	}

	public static int ToIndex(int file, int rank) {
		return 8 * rank + file;
	}
}
