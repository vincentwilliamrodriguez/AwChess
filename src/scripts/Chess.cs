using Godot;
using System;

public partial class Chess {
	public ulong[,] pieces = new ulong[2,6];
	
	public Chess() {
		GD.Print("g awaw");
		g.PrintBitboard(pieces);
	}

	public void ImportFromFEN(string FEN){
		// rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1

		string[] fields = FEN.Split(' ');
	}
}
