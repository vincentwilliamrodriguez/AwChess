using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class main : Node2D
{
	public Script Chess;
	public Chess cur;
	public TileMap board_pieces_node;
	public ColorRect turnIndicator;
	public Sprite2D promotionBackground;
	public GodotThread AwChess;
	// public int n = 0;
	
	public override void _Ready()
	{
		board_pieces_node = (TileMap) GetNode("Board_Pieces");
		turnIndicator = (ColorRect) GetNode("TurnIndicator");
		promotionBackground = (Sprite2D) GetNode("PromotionBackground");
		Chess = (Script) GD.Load("res://src/scripts/Chess.cs");
		cur = new Chess();
		
		g.Init();
		// cur.ImportFromFEN(g.startingPosition);
		cur.ImportFromFEN("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
		

		InitBoard();
		UpdatePieces();	

		Action perft = () => {GetPerft(2);};
		AwChess = new GodotThread();
		AwChess.Start(Callable.From(perft));
	}

	public override void _Process(double delta) {
		UpdatePieces();
		HighlightPossibleMoves();
		// if (cur.pinnedPieces != 0UL)
			// HighlightBitboard(ulong.MaxValue);
		// n++;
		if (cur.b.gameOutcome == -1 && !g.isPlayer[cur.b.sideToMove])
		{
			// System.Threading.Thread.Sleep(500);

			var watch = System.Diagnostics.Stopwatch.StartNew();

			RunAwChess();

			watch.Stop();
			// GD.Print(watch.ElapsedMilliseconds);
		}
		

	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && Input.IsActionJustReleased("click"))
		{
			Vector2I coor = board_pieces_node.LocalToMap(mouseEvent.Position);

			if (g.isPromoting && g.isPlayer[cur.b.sideToMove] && cur.b.gameOutcome == -1)
			{
				// end choosing promotion piece
				Vector2I promotionAtlas = board_pieces_node.GetCellAtlasCoords(4, coor);
				int promotionPiece = promotionAtlas.X;

				if (promotionPiece != -1) // if player didn't click on non-promotion pieces squares
				{
					cur.MakeMove(g.selectedPiece, g.promotionTarget, promotionPiece);
					UpdatePieces();
				}
				
				g.promotionTarget = -1;
				g.isPromoting = false;

				UpdatePromotionDisplay();
			}
			else if (g.IsWithinBoard(coor.X, coor.Y) && g.isPlayer[cur.b.sideToMove] && cur.b.gameOutcome == -1)
			{
				/* HUMAN */
				coor.X = g.CanFlip(coor.X);
				coor.Y = g.CanFlip(coor.Y);
				coor.Y = 7 - coor.Y;

				int targetIndex = g.ToIndex(coor.X, coor.Y);
				bool isTargetOccupied = Convert.ToBoolean(
										cur.b.occupancyByColor[cur.b.sideToMove] >> targetIndex
										& 1UL); // checking if target square has a same color piece

				if (!g.isMovingPiece && isTargetOccupied)
				{
					g.selectedPiece = targetIndex;
					g.selectedPieceN = cur.FindPieceN(g.selectedPiece);
					ulong PieceMoves = cur.b.possibleMoves[g.selectedPieceN][g.selectedPiece];

					if (PieceMoves != 0UL)
					{
						g.curHighlightedMoves = cur.b.possibleMoves[g.selectedPieceN][g.selectedPiece];
						g.isMovingPiece = true;
					}
				}

				else if (g.isMovingPiece)
				{
					ulong pieceMoves = cur.b.possibleMoves[g.selectedPieceN][g.selectedPiece];
					if ((pieceMoves >> targetIndex & 1UL) == 1) // Checks if move is part of generated moves
					{
						g.isPromoting = g.selectedPieceN == 5 && // is a pawn
										(targetIndex / 8) == g.promotionRank[cur.b.sideToMove]; // target is promotion rank
						
						if (!g.isPromoting)
						{
							cur.MakeMove(g.selectedPiece, targetIndex);
							UpdatePieces();
						}
						else
						{
							// start choosing promotion piece
							g.promotionTarget = targetIndex;
							UpdatePromotionDisplay();
						}
					}

					g.isMovingPiece = false;
					g.curHighlightedMoves = 0UL;
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
		
		Color turnColor = (cur.b.sideToMove == 0) ? new Color(1, 1, 1, 1) : new Color(0, 0, 0, 1);
		turnIndicator.Color = turnColor;

		for (int colorN = 0; colorN < 2; colorN++) {
			for (int pieceN = 0; pieceN < 6; pieceN++) {
				ulong pieceBits = cur.b.pieces[colorN, pieceN];

				for (int i = 0; i < 64; i++) {
					if (Convert.ToBoolean((pieceBits >> i) & 1UL)) { // checks if pieceBits[i] is 1 or true, then places the piece
						int x = g.CanFlip(i % 8);
						int y = g.CanFlip(7 - (i / 8));
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
				int x = g.CanFlip(i % 8);
				int y = g.CanFlip(7 - (i / 8));
				board_pieces_node.SetCell(2, new Vector2I(x, y), 3, new Vector2I(0, 0));
			}

			bitboardTemp = bitboardTemp >> 1;
		}
	}

	public void HighlightPossibleMoves() {
		board_pieces_node.ClearLayer(3);
		ulong silentMoves = g.curHighlightedMoves & ~cur.b.occupancy;
		ulong captureMoves = g.curHighlightedMoves & cur.b.occupancy;

		foreach (int i in cur.b.lastMove)
		{
			if (i != -1)
			{
				int x = g.CanFlip(i % 8);
				int y = g.CanFlip(7 - (i / 8));
				board_pieces_node.SetCell(3, new Vector2I(x, y), 4, new Vector2I(0, 0));
			}
		}

		foreach (int i in g.Serialize(silentMoves))
		{
			int x = g.CanFlip(i % 8);
			int y = g.CanFlip(7 - (i / 8));
			board_pieces_node.SetCell(3, new Vector2I(x, y), 3, new Vector2I(0, 0));
		}

		
		foreach (int i in g.Serialize(captureMoves))
		{
			int x = g.CanFlip(i % 8);
			int y = g.CanFlip(7 - (i / 8));
			board_pieces_node.SetCell(3, new Vector2I(x, y), 5, new Vector2I(0, 0));
		}

		if (cur.b.isInCheck)
		{
			int x = g.CanFlip(cur.b.kingPos % 8);
			int y = g.CanFlip(7 - (cur.b.kingPos / 8));
			board_pieces_node.SetCell(3, new Vector2I(x, y), 5, new Vector2I(0, 0));
		}
	}

	public void UpdatePromotionDisplay()
	{
		if (g.isPromoting)
		{
			board_pieces_node.SetCell(4, new Vector2I(8, 0), 2, new Vector2I(1, cur.b.sideToMove)); // queen
			board_pieces_node.SetCell(4, new Vector2I(9, 0), 2, new Vector2I(4, cur.b.sideToMove)); // rook
			board_pieces_node.SetCell(4, new Vector2I(8, 1), 2, new Vector2I(2, cur.b.sideToMove)); // bishop
			board_pieces_node.SetCell(4, new Vector2I(9, 1), 2, new Vector2I(3, cur.b.sideToMove)); // knight
			promotionBackground.Show();
		}
		else
		{
			board_pieces_node.ClearLayer(4);
			promotionBackground.Hide();
		}
		
		
	}



	/* AWCHESS CHESS ENGINE */

	public void RunAwChess()
	{
		/* AWCHESS */
		List<int[]> flattenedMoves = cur.flattenPossibleMoves();

		Random random = new Random();
		int randomMove = random.Next(flattenedMoves.Count);
		int randomStart = flattenedMoves[randomMove][0];
		int randomEnd = flattenedMoves[randomMove][1];
		int randomPromotion = g.promotionPieces[random.Next(4)];

		cur.MakeMove(randomStart, randomEnd, randomPromotion);
		UpdatePieces();
		HighlightPossibleMoves();
	}

	public void GetPerft(int depth)
	{
		GD.Print("Total number of positions: ", Perft(depth));
	}

	public int Perft(int depth)
	{
		if (depth == 0)
		{
			return 1;
		}

		int nodes = 0;
		Board curB = cur.b;
		List<int[]> flattenedMoves = cur.flattenPossibleMoves();
		// GD.Print("Depth ", depth, "  ", flattenedMoves.Count);

		foreach (int[] move in flattenedMoves)
		{

			cur.MakeMove(move[0], move[1], 1);
			System.Threading.Thread.Sleep(5);

			nodes += Perft(depth - 1);

			cur.UnmakeMove(move[0], move[1], curB);
			cur.b = curB;
			System.Threading.Thread.Sleep(5);
		}

		return nodes;
	}
}

/* 
var watch = System.Diagnostics.Stopwatch.StartNew();
		
watch.Stop();
GD.Print(watch.ElapsedMilliseconds);
 */