using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class main : Node2D
{
	public Chess cur;
	public AwChess[] AwChessBot = new AwChess[2];
	public GodotThread[] AwChessThread = new GodotThread[2];

	public TileMap boardPiecesNode;
	public ColorRect turnIndicator;
	public Sprite2D promotionBackground;
	public Label debugLabelNode;
	
	public override void _Ready()
	{
		boardPiecesNode = (TileMap) GetNode("Board_Pieces");
		turnIndicator = (ColorRect) GetNode("TurnIndicator");
		promotionBackground = (Sprite2D) GetNode("PromotionBackground");
		debugLabelNode = (Label) GetNode("DebugLabel");
		
		g.Init();

		cur = new Chess();
		// cur.ImportFromFEN(g.startingPosition);
		cur.ImportFromFEN("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
	
		InitBoard();
		UpdatePieces();
		g.staticEvaluation = cur.Evaluate();

		/* AwChess Bot Generation */
		for (int colorN = 0; colorN < 2; colorN++)
		{
			int tempColorN = colorN;
			Action joinThreadAct = () => {JoinThread(tempColorN);}; // NOTE: tempColorN prevents thread bug regarding variable as parameter

			AwChessBot[colorN] = new AwChess(colorN, ref cur, Callable.From(joinThreadAct));
			AwChessThread[colorN] = new GodotThread();

			if (!g.isPlayer[colorN])
			{
				// ActivateAwChessPerft(colorN);
			}
		}

		// g.PrintMoveList(cur.b.possibleMoves);
		// g.PrintMoveList(g.OrderMoves(cur.b.possibleMoves, cur));
	}

	public override void _Process(double delta) {
		UpdatePieces();
		HighlightPossibleMoves();
		g.debugLabel = String.Format("Outcome: {0}\nEvaluation: {1}", 
									 Convert.ToString(cur.b.gameOutcome),
									 g.staticEvaluation);
		debugLabelNode.Text = g.debugLabel;

		// Random random = new Random();
		HighlightBitboard(g.testHighlight);

		int turn = cur.b.sideToMove;

		if (!g.isPlayer[turn] && 					// AwChess bot's turn
			!AwChessThread[1 - turn].IsAlive() &&	// Opponent bot not running
			cur.b.gameOutcome == -1)				// Game not ended yet
		{
			/* Not Thinking */
			if (!AwChessThread[turn].IsAlive())
			{
				Action botMove = () => {AwChessBot[turn].SearchMove();};
				AwChessBot[turn].UpdateCopy();
				AwChessThread[turn].Start(Callable.From(botMove));
			}

			/* Thinking */
			else
			{

			}
			// System.Threading.Thread.Sleep(500);
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (Input.IsActionJustReleased("click"))
			{
				g.perftSpeed = 0;
				Vector2I coor = boardPiecesNode.LocalToMap(mouseEvent.Position);

				if (g.isPromoting && g.isPlayer[cur.b.sideToMove] && cur.b.gameOutcome == -1)
				{
					// end choosing promotion piece
					Vector2I promotionAtlas = boardPiecesNode.GetCellAtlasCoords(4, coor);
					int promotionPiece = promotionAtlas.X;

					if (promotionPiece != -1) // if player didn't click on non-promotion pieces squares
					{
						MakePlayerMove(g.promotionTarget, promotionPiece);
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
						ulong PieceMoves = cur.b.possibleMovesBB[g.selectedPieceN][g.selectedPiece];

						if (PieceMoves != 0UL)
						{
							g.curHighlightedMoves = cur.b.possibleMovesBB[g.selectedPieceN][g.selectedPiece];
							g.isMovingPiece = true;
						}
					}

					else if (g.isMovingPiece)
					{
						ulong pieceMoves = cur.b.possibleMovesBB[g.selectedPieceN][g.selectedPiece];
						if ((pieceMoves >> targetIndex & 1UL) == 1) // Checks if move is part of generated moves
						{
							g.isPromoting = g.selectedPieceN == 5 && // is a pawn
											(targetIndex / 8) == g.promotionRank[cur.b.sideToMove]; // target is promotion rank
							
							if (!g.isPromoting)
							{
								MakePlayerMove(targetIndex);
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
			else if (Input.IsActionJustPressed("click"))
			{
				g.perftSpeed = 50;
			}
		}
	}

	public void MakePlayerMove(int targetIndex, int promotionPiece = -1)
	{
		Move playerMove = new Move();

		foreach (Move move in cur.b.possibleMoves)
		{
			if (move.pieceN == g.selectedPieceN && move.start == g.selectedPiece && move.end == targetIndex)
			{
				playerMove = move;
			}
		}
		
		playerMove.promotionPiece = promotionPiece;
		cur.MakeMove(playerMove);

		g.staticEvaluation = cur.Evaluate();
	}

	public void InitBoard() {
		for (int i = 7; i >= 0; i--)
		{
			for (int j = 0; j < 8; j++)
			{
				if ((i + j) % 2 == 0)
				{
					boardPiecesNode.SetCell(0, new Vector2I(i, j), 0, new Vector2I(0, 0));
				} else {
					boardPiecesNode.SetCell(0, new Vector2I(i, j), 1, new Vector2I(0, 0));
				}
			}
		}
	}

	public void UpdatePieces() {
		boardPiecesNode.ClearLayer(1);
		
		Color turnColor = (cur.b.sideToMove == 0) ? new Color(1, 1, 1, 1) : new Color(0, 0, 0, 1);
		turnIndicator.Color = turnColor;

		for (int colorN = 0; colorN < 2; colorN++) {
			for (int pieceN = 0; pieceN < 6; pieceN++) {
				ulong pieceBits = cur.b.pieces[colorN, pieceN];

				for (int i = 0; i < 64; i++) {
					if (Convert.ToBoolean((pieceBits >> i) & 1UL)) { // checks if pieceBits[i] is 1 or true, then places the piece
						int x = g.CanFlip(i % 8);
						int y = g.CanFlip(7 - (i / 8));
						boardPiecesNode.SetCell(1, 					// 2nd Layer (pieces)
												  new Vector2I(x, y), 	// Coordinate in the chessboard
												  2, 					// ID of pieces.png
												  new Vector2I(pieceN, colorN)); // Atlas coordinates of piece
					}
				}
			}
		}
	}

	public void HighlightBitboard(ulong bitboard) {
		boardPiecesNode.ClearLayer(2);

		ulong bitboardTemp = bitboard;
		for (int i = 0; i < 64; i++)
		{
			if ((bitboardTemp & 1UL) == 1) {
				int x = g.CanFlip(i % 8);
				int y = g.CanFlip(7 - (i / 8));
				boardPiecesNode.SetCell(2, new Vector2I(x, y), 3, new Vector2I(0, 0));
			}

			bitboardTemp = bitboardTemp >> 1;
		}
	}

	public void HighlightPossibleMoves() {
		boardPiecesNode.ClearLayer(3);
		ulong silentMoves = g.curHighlightedMoves & ~cur.b.occupancy;
		ulong captureMoves = g.curHighlightedMoves & cur.b.occupancy;

		foreach (int i in new int[2] {cur.b.lastMove.start, cur.b.lastMove.end})
		{
			if (i != -1)
			{
				int x = g.CanFlip(i % 8);
				int y = g.CanFlip(7 - (i / 8));
				boardPiecesNode.SetCell(3, new Vector2I(x, y), 4, new Vector2I(0, 0));
			}
		}

		foreach (int i in g.Serialize(silentMoves))
		{
			int x = g.CanFlip(i % 8);
			int y = g.CanFlip(7 - (i / 8));
			boardPiecesNode.SetCell(3, new Vector2I(x, y), 3, new Vector2I(0, 0));
		}

		
		foreach (int i in g.Serialize(captureMoves))
		{
			int x = g.CanFlip(i % 8);
			int y = g.CanFlip(7 - (i / 8));
			boardPiecesNode.SetCell(3, new Vector2I(x, y), 5, new Vector2I(0, 0));
		}

		if (cur.b.isInCheck)
		{
			int x = g.CanFlip(cur.b.kingPos % 8);
			int y = g.CanFlip(7 - (cur.b.kingPos / 8));
			boardPiecesNode.SetCell(3, new Vector2I(x, y), 5, new Vector2I(0, 0));
		}
	}

	public void UpdatePromotionDisplay()
	{
		if (g.isPromoting)
		{
			boardPiecesNode.SetCell(4, new Vector2I(8, 0), 2, new Vector2I(1, cur.b.sideToMove)); // queen
			boardPiecesNode.SetCell(4, new Vector2I(9, 0), 2, new Vector2I(4, cur.b.sideToMove)); // rook
			boardPiecesNode.SetCell(4, new Vector2I(8, 1), 2, new Vector2I(2, cur.b.sideToMove)); // bishop
			boardPiecesNode.SetCell(4, new Vector2I(9, 1), 2, new Vector2I(3, cur.b.sideToMove)); // knight
			promotionBackground.Show();
		}
		else
		{
			boardPiecesNode.ClearLayer(4);
			promotionBackground.Hide();
		}
	}

	public void ActivateAwChessPerft(int color)
	{
		Action perft = () => {AwChessBot[color].GetPerft(g.perftDepth);};
		AwChessThread[color].Start(Callable.From(perft));
	}

	public void JoinThread(int color)
	{
		AwChessThread[color].WaitToFinish();
	}
}

/* 
var watch = System.Diagnostics.Stopwatch.StartNew();
		
watch.Stop();
GD.Print(watch.ElapsedMilliseconds);
 */