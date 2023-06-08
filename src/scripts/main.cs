using Godot;
using System;

public partial class main : Node2D
{
	public TileMap board_pieces_node;
	public Script Chess;
	
	public override void _Ready()
	{
		board_pieces_node = (TileMap) GetNode("Board_Pieces");
		Chess = (Script) GD.Load("res://src/scripts/Chess.cs");
		var current = new Chess();

		current.ImportFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
		g.PrintBitboard(current.pieces);

		InitBoard();
	}

	public override void _Process(double delta)
	{
		
	}

	public void InitBoard(){
		for (int i = 7; i >= 0; i--)
		{
			for (int j = 0; j < 8; j++)
			{
				if ((i + j) % 2 == 0)
				{
					board_pieces_node.SetCell(0, new Vector2I(i, j), 0, new Vector2I(0, 0));
				} else {
					board_pieces_node.SetCell(0, new Vector2I(i, j), 1, new Vector2I(0, 0));
				}
			}
		}
	}

	public void UpdatePieces(){
		foreach (ulong[] colorPieces in current.pieces)
		{
			foreach (ulong pieceBits in colorPieces) // REMINDER: REVISE
			{

			}
		}
	}
}
