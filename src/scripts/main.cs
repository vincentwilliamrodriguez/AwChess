using Godot;
using System;

public partial class main : Node2D
{
	public TileMap board_pieces_node;
	public Script Chess;
	public Chess cur;
	
	public override void _Ready()
	{
		board_pieces_node = (TileMap) GetNode("Board_Pieces");
		Chess = (Script) GD.Load("res://src/scripts/Chess.cs");
		cur = new Chess();

		cur.ImportFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
		g.InitRayAttacks();

		InitBoard();
		UpdatePieces();	
	}

	public override void _Process(double delta) {
		HighlightBitboard(g.curPossibleMoves);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && Input.IsActionJustReleased("click"))
		{
			Vector2I coor = board_pieces_node.LocalToMap(mouseEvent.Position);
			
			if (g.IsWithinBoard(coor.X, coor.Y))
			{
				int sideToMove = cur.sideToMove;
				coor.Y = 7 - coor.Y;
				int targetIndex = g.ToIndex(coor.X, coor.Y);
				bool isTargetOccupied = Convert.ToBoolean(
										cur.occupancyByColor[sideToMove] >> targetIndex
										& 1UL); // checking if target square has a same color piece

				if (!g.isMovingPiece && isTargetOccupied)
				{
					g.selectedPiece = targetIndex;
					g.curPossibleMoves = cur.GenerateMovesByIndex(g.selectedPiece);

					if (g.curPossibleMoves != 0UL)
					{
						g.isMovingPiece = true;
					}
				}

				else if (g.isMovingPiece)
				{
					if ((g.curPossibleMoves >> targetIndex & 1UL) == 1) // Checks if move is part of generated moves
					{
						cur.MakeMove(g.selectedPiece, targetIndex);
					}

					g.curPossibleMoves = 0;
					g.isMovingPiece = false;
					UpdatePieces();
				}
			}
		}
	}

	public void InitBoard() {
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
		board_pieces_node.ClearLayer(1);

		for (int colorN = 0; colorN < 2; colorN++) {
			for (int pieceN = 0; pieceN < 6; pieceN++) {
				ulong pieceBits = cur.pieces[colorN, pieceN];

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

	public void HighlightBitboard(ulong bitboard) {
		board_pieces_node.ClearLayer(2);

		ulong bitboardTemp = bitboard;
		for (int i = 0; i < 64; i++)
		{
			if ((bitboardTemp & 1UL) == 1) {
				int x = i % 8;
				int y = 7 - (i / 8);
				board_pieces_node.SetCell(2, new Vector2I(x, y), 3, new Vector2I(0, 0));
			}

			bitboardTemp = bitboardTemp >> 1;
		}
	}
}

/* 
var watch = System.Diagnostics.Stopwatch.StartNew();
		
watch.Stop();
GD.Print(watch.ElapsedMilliseconds);
 */