using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class main : Node2D
{
	public Chess cur;
	public List<Board> curBoardHistory = new List<Board> {};
	public AwChess[] AwChessBot = new AwChess[2];
	public GodotThread[] AwChessThread = new GodotThread[2];

	public TileMap boardPiecesNode;
	public ColorRect turnIndicator;
	public Sprite2D promotionBackground;
	public Label debugLabelNode;
	public TileMap movingNode;
	
	public override void _Ready()
	{
		boardPiecesNode = (TileMap) GetNode("Board_Pieces");
		turnIndicator = (ColorRect) GetNode("TurnIndicator");
		promotionBackground = (Sprite2D) GetNode("PromotionBackground");
		debugLabelNode = (Label) GetNode("DebugLabel");
		movingNode = (TileMap) GetNode("Moving");
		
		g.mainNode = GetNode(".");
		g.Init();

		cur = new Chess();
		cur.ImportFromFEN(g.startingPosition);
		// cur.ImportFromFEN("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
	
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
		int turn = cur.b.sideToMove;

		UpdatePieces();
		HighlightPossibleMoves();
		g.debugLabel = String.Format("Outcome: {0}\nEvaluation: {1}\nBot Depth: {2}\nTime: {3} s\nCount: {4}", 
									 Convert.ToString(cur.b.gameOutcome),
									 g.staticEvaluation,
									 !g.isPlayer[turn] ? AwChessBot[turn].IDdepth : "N/A",
									 AwChessBot[turn].time.ElapsedMilliseconds / 1000.0,
									 AwChessBot[turn].count);
		debugLabelNode.Text = g.debugLabel;

		// Random random = new Random();
		HighlightBitboard(g.testHighlight);

		if (!g.isPlayer[turn] && 					// AwChess bot's turn
			!AwChessThread[1 - turn].IsAlive() &&	// Opponent bot not running
			cur.b.gameOutcome == -1)				// Game not ended yet
		{
			/* Not Thinking */
			if (!AwChessThread[turn].IsAlive())
			{
				curBoardHistory.Add(cur.b.Copy());

				Action botMove = () => {AwChessBot[turn].SearchMove();};
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

		if (Input.IsActionJustReleased("undo"))
		{
			if (!(AwChessThread[0].IsAlive() || AwChessThread[1].IsAlive()) && curBoardHistory.Any())
			{
				Board lastBoard = curBoardHistory.Last(); // retrieves last board state
				curBoardHistory.RemoveAt(curBoardHistory.Count - 1); // removes said state from history

				cur.UnmakeMove(cur.b.lastMove, ref lastBoard);
				cur.b = lastBoard.Copy();

				g.isMovingPiece = false;
				g.selectedPiece = -1; // [piece, index]
				g.selectedPieceN = -1;
				g.curHighlightedMoves = 0UL;
				g.staticEvaluation = cur.Evaluate();
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

		curBoardHistory.Add(cur.b.Copy());
		cur.MakeMove(playerMove);
		MovingAnimation();


		g.staticEvaluation = cur.Evaluate();
		GD.Print(g.MoveToString(playerMove), '\n');
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
					if (Convert.ToBoolean((pieceBits >> i) & 1UL)) // checks if pieceBits[i] is 1 or true, then places the piece
					{
						boardPiecesNode.SetCell(1, 								// 2nd Layer (pieces)
												g.IndexToVector(i), 			// Coordinate in the chessboard
												2, 								// ID of pieces.png
												new Vector2I(pieceN, colorN));	// Atlas coordinates of piece
					}

					/* Moving Animation */
					if (g.handlePos == i)
					{
						boardPiecesNode.SetCell(1, 
												g.IndexToVector(i), 
												2, 
												new Vector2I(g.handlePieceN, g.handleColorN));
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
				boardPiecesNode.SetCell(2, g.IndexToVector(i), 3, new Vector2I(0, 0));
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
				boardPiecesNode.SetCell(3, g.IndexToVector(i), 4, new Vector2I(0, 0));
			}
		}

		foreach (int i in g.Serialize(silentMoves))
		{
			boardPiecesNode.SetCell(3, g.IndexToVector(i), 3, new Vector2I(0, 0));
		}

		
		foreach (int i in g.Serialize(captureMoves))
		{
			boardPiecesNode.SetCell(3, g.IndexToVector(i), 5, new Vector2I(0, 0));
		}

		if (cur.b.isInCheck)
		{
			boardPiecesNode.SetCell(3, g.IndexToVector(cur.b.kingPos), 5, new Vector2I(0, 0));
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

	public void MovingAnimation()
	{
		Move move = cur.b.lastMove;
		int colorN = 1 - cur.b.sideToMove;

		Tween moveTween = GetTree().CreateTween();
		Action resetHandle = () => 
		{
			g.handlePos = -1;
			g.handlePieceN = -1;
			g.handleColorN = -1;
			UpdatePieces();
			movingNode.Clear();
			movingNode.Position = new Vector2(0, 0);
		};

		g.handlePos = move.end;
		g.handlePieceN = move.capturedPiece;
		g.handleColorN = 1 - colorN;

		Vector2I startVector = g.IndexToVector(move.start);
		Vector2I endVector = g.IndexToVector(move.end);
		Vector2 diff = (Vector2) (133 * (endVector - startVector));
		double speed = g.moveSpeed * diff.Length() / 133.0;

		movingNode.SetCell(0, startVector, 0, new Vector2I(move.pieceN, colorN));

		moveTween.TweenProperty(movingNode, "position", diff, speed).SetTrans(Tween.TransitionType.Sine);
		moveTween.TweenCallback(Callable.From(resetHandle));
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