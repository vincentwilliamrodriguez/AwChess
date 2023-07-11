using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;

public partial class main : Node2D
{
	public Chess cur;
	public AwChess[] AwChessBot = new AwChess[2];
	public GodotThread[] AwChessThread = new GodotThread[2];

	public TileMap boardPiecesNode;
	public ColorRect turnIndicator;
	public Sprite2D promotionBackground;
	public Label debugLabelNode;
	public TileMap movingNodeTemplate;
	public Sprite2D[] highlightNodes = new Sprite2D[2];
	public Label[] labelNodes = new Label[2];

	public bool interrupt = false;
	
	public override void _Ready()
	{
		boardPiecesNode = (TileMap) GetNode("Board_Pieces");
		turnIndicator = (ColorRect) GetNode("TurnIndicator");
		promotionBackground = (Sprite2D) GetNode("PromotionBackground");
		debugLabelNode = (Label) GetNode("DebugLabel");
		movingNodeTemplate = (TileMap) GetNode("Moving");
		
		g.mainNode = GetNode(".");
		g.Init();

		cur = new Chess();
		cur.isCur = true;
		cur.ImportFromFEN(g.startingPosition);
		// cur.ImportFromFEN("4r2r/2k3p1/Ppp2p1p/7P/2pP2P1/2B2P2/5K2/R3R3 w - - 0 35");
	
		InitBoard();
		g.UpdatePiecesDisplay(cur.b);
		UpdatePieces();
		g.staticEvaluation = cur.Evaluate(true);

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

		/* Player and Bot Icons */
		for (int colorN = 0; colorN < 2; colorN++)
		{
			string c = Convert.ToString(colorN);
			highlightNodes[colorN] = (Sprite2D) GetNode("Highlight" + c);
			labelNodes[colorN] = (Label) GetNode("Label" + c);

			Sprite2D playerSprite = (Sprite2D) GetNode((g.isPlayer[colorN] ? "Player" : "Bot") + c);

			if (g.isBoardFlipped)
			{
				playerSprite.Position = new Vector2(playerSprite.Position.X, (colorN == 0) ? 0 : 814);
				highlightNodes[colorN].Position = new Vector2(highlightNodes[colorN].Position.X, (colorN == 0) ? 14 : 827);
				labelNodes[colorN].Position = new Vector2(labelNodes[colorN].Position.X, (colorN == 0) ? 248 : 575);
			}

			playerSprite.Show();
		}

		// g.PrintMoveList(cur.b.possibleMoves);
		// g.PrintMoveList(g.OrderMoves(cur.b.possibleMoves, cur));

		/* 
		var thread = new GodotThread();
		Action import = () => {ParseOpenings();};
		thread.Start(Callable.From(import));
 		 */
	}

	public override void _Process(double delta) {
		int turn = cur.b.sideToMove;

		if (cur.b.gameOutcome != -1)
		{
			interrupt = true;
		}

		for (int colorN = 0; colorN < 2; colorN++)
		{
			AwChess bot = AwChessBot[colorN];
			
			/* Interruption */
			if (!g.isPlayer[colorN])
			{
				bot.interrupt = interrupt;
			}

			/* Turn Highlight Indicator */
			highlightNodes[colorN].Visible = cur.b.sideToMove == colorN;

			/* Label */
			if (g.isPlayer[colorN])
			{
				labelNodes[colorN].Text = String.Format("Outcome: {0}\nEvaluation: {1}\n", 
														cur.b.gameOutcome,
														g.staticEvaluation);
			}
			else
			{
				labelNodes[colorN].Text = String.Format("Expected: {0}\nEvaluation: {1}\nBot Depth: {2}\nTime: {3} s\nCount: {4}", 
														bot.expectedOpponentMoveDisplay,
														bot.botEval * g.sign[colorN],
														bot.IDdepth,
														bot.time.ElapsedMilliseconds / 1000.0,
														bot.count);
			}

			/* Pondering */
			if (!g.isPlayer[colorN] && 					// AwChess bot
			!AwChessThread[colorN].IsAlive() &&			// Bot not thinking
			!interrupt)									// Game not ended yet
			{
				Action botMove = () => {bot.SearchMove();};
				AwChessThread[colorN].Start(Callable.From(botMove));
			}
			
			/* Starting Timer */
			if (!g.isPlayer[colorN] &&
				cur.b.sideToMove == colorN &&
				!bot.time.IsRunning &&
				!interrupt)
			{
				bot.unexpectedMove = !bot.expectedOpponentMove.Equals(cur.b.lastMove);
				bot.time = Stopwatch.StartNew();
			}
		}

		UpdatePieces();
		HighlightPossibleMoves();
		HighlightBitboard(g.testHighlight);

		/* 
		g.debugLabel = String.Format("Outcome: {0}\nEvaluation: {1}\nBot Depth: {2}\nTime: {3} s\nCount: {4}", 
									 Convert.ToString(cur.b.gameOutcome),
									 AwChessBot[botIndex].botEval * g.sign[botIndex],
									 !g.isPlayer[turn] ? AwChessBot[turn].IDdepth : "N/A",
									 AwChessBot[turn].time.ElapsedMilliseconds / 1000.0,
									 AwChessBot[turn].count);
		debugLabelNode.Text = g.debugLabel;
 		*/
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (Input.IsActionJustReleased("click"))
			{
				g.perftSpeed = 0;
				Vector2I coor = boardPiecesNode.LocalToMap(mouseEvent.Position);

				if (g.isPromoting && g.isPlayer[cur.b.sideToMove] && !interrupt)
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
				else if (g.IsWithinBoard(coor.X, coor.Y) && g.isPlayer[cur.b.sideToMove] && !interrupt)
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

				GD.Print("\nAWAW");
				GD.Print(cur.ExportToPGN());
			}
		}

		if (Input.IsActionJustReleased("undo"))
		{
			if (!(AwChessThread[0].IsAlive() || AwChessThread[1].IsAlive()) && cur.boardHistory.Any())
			{
				Board lastBoard = cur.boardHistory.Last(); // retrieves last board state
				cur.boardHistory.RemoveAt(cur.boardHistory.Count - 1); // removes said state from history

				cur.UnmakeMove(cur.b.lastMove, lastBoard);
				cur.b = lastBoard.Copy();

				g.isMovingPiece = false;
				g.selectedPiece = -1; // [piece, index]
				g.selectedPieceN = -1;
				g.curHighlightedMoves = 0UL;
				g.staticEvaluation = cur.Evaluate(true);
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
		GD.Print(g.MoveToString(playerMove, cur.b.possibleMoves), '\n');

		cur.MakeMove(playerMove);

		g.UpdatePiecesDisplay(cur.b, playerMove, 1 - cur.b.sideToMove);
		MovingAnimation();


		g.staticEvaluation = cur.Evaluate(true);
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
				ulong pieceBits = g.piecesDisplay[colorN, pieceN];

				for (int i = 0; i < 64; i++) {
					if (Convert.ToBoolean((pieceBits >> i) & 1UL)) // checks if pieceBits[i] is 1 or true, then places the piece
					{
						boardPiecesNode.SetCell(1, 								// 2nd Layer (pieces)
												g.IndexToVector(i), 			// Coordinate in the chessboard
												2, 								// ID of pieces.png
												new Vector2I(pieceN, colorN));	// Atlas coordinates of piece
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
		int colorN = cur.FindColorN(move.end);
		MovingAnimationByPiece(move, colorN);

		/* Moving Rook for Castling */
		if (move.pieceN == 0 && (move.start % 8) == 4)
		{
			for (int sideN = 0; sideN < 2; sideN++)
			{
				if (move.end == g.castlingKingPos[colorN, sideN])
				{
					move = new Move(4, 										// rook
									g.castlingRookPosFrom[colorN, sideN],	// rook start
									g.castlingRookPosTo[colorN, sideN]);	// rook end
					MovingAnimationByPiece(move, colorN);
				}
			}
		}
	}

	public void MovingAnimationByPiece(Move move, int colorN)
	{
		TileMap movingNode = (TileMap) movingNodeTemplate.Duplicate();
		AddChild(movingNode);

		Tween moveTween = GetTree().CreateTween();
		Action resetHandle = () => 
		{
			g.UpdatePiecesDisplay(cur.b);
			UpdatePieces();
			movingNode.QueueFree();
		};

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

	public void ParseOpenings()
	{
		using var file = FileAccess.Open("user://twic1495.pgn", FileAccess.ModeFlags.Read);
		using var file2 = FileAccess.Open("user://opening_book.json", FileAccess.ModeFlags.Write);

		string[] games = file.GetAsText().Split("\n\n[", StringSplitOptions.TrimEntries);
		var posMoves = new Dictionary<ulong, Dictionary<string, int>> {};
		int n = 0;
		foreach (string item in games)
		{
			n++;
			string game = item;

			if (item[0] != '[')
			{
				game = "[" + game;
			}

			cur.ImportFromPGN(game, true, ref posMoves);
		}

		GD.Print("AWAW ", posMoves.Count);

		string posMovesSerialized = JsonSerializer.Serialize(posMoves);
		file2.StoreString(posMovesSerialized);
	}
}

/* 
var watch = System.Diagnostics.Stopwatch.StartNew();
		
watch.Stop();
GD.Print(watch.ElapsedMilliseconds);
 */

 /* 
var thread = new GodotThread();
Action import = () => {cur.ImportFromPGN(game);};
thread.Start(Callable.From(import));
  */