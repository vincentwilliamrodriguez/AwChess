using Godot;
using System;

public partial class main : Node2D
{
	public TileMap board_pieces_node;
	public Script Chess;
	public Chess currentBoard;
	
	public override void _Ready()
	{
		board_pieces_node = (TileMap) GetNode("Board_Pieces");
		Chess = (Script) GD.Load("res://src/scripts/Chess.cs");
		currentBoard = new Chess();

		currentBoard.ImportFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
		// g.PrintBitboard(currentBoard.pieces);
		foreach (ulong bits in currentBoard.pieces) {
			GD.Print(Convert.ToString((long) bits, 2));
		}

		InitBoard();
		UpdatePieces();
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

	public void UpdatePieces() {
		for (int colorN = 0; colorN < 2; colorN++) {
			for (int pieceN = 0; pieceN < 6; pieceN++) {
				ulong pieceBits = currentBoard.pieces[colorN, pieceN];

				for (int i = 0; i < 64; i++) {
					if (Convert.ToBoolean((pieceBits >> i) & 1UL)) { // checks if pieceBits[i] is 1 or true, then places the piece
						int x = i % 8;
						int y = 7 - (i / 8);
						board_pieces_node.SetCell(1, 					// 2nd Layer (pieces)
												  new Vector2I(x, y), 	// Coordinate in the chessboard
												  2, 					// ID of pieces.png
												  new Vector2I(pieceN, colorN)); // Atlas coordinates of piece
					}
				}
			}
		}
	}
}
