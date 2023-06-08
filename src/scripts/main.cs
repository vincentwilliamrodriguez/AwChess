using Godot;
using System;

public partial class main : Node2D
{

	public override void _Ready()
	{
		var Chess = (Script) GD.Load("res://src/scripts/Chess.cs");
		var current = new Chess();

		current.ImportFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
		g.PrintBitboard(current.pieces);
	}

	public override void _Process(double delta)
	{
		
	}
}
