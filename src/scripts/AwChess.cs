using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class AwChess : Node
{
    public Chess curRef;
    public Chess curCopy;
    public Callable mainJoinThread;
    public int botColor;

    public AwChess(int color, ref Chess source, Callable joinThread)
    {
        curRef = source;
        botColor = color;
        mainJoinThread = joinThread;

        UpdateCopy();
    }

    public void UpdateCopy()
    {
        curCopy = curRef.Copy();
    }

    /* AWCHESS CHESS ENGINE */

	public void RandomMove()
	{

		List<Move> flattenedMoves = curCopy.b.possibleMoves;

		int randomMove = g.random.Next(flattenedMoves.Count);
		int randomPiece = flattenedMoves[randomMove].pieceN;
		int randomStart = flattenedMoves[randomMove].start;
		int randomEnd = flattenedMoves[randomMove].end;
		int randomPromotion = g.promotionPieces[g.random.Next(4)];

        System.Threading.Thread.Sleep(g.botSpeed);

		curRef.MakeMove(new Move(randomPiece, randomStart, randomEnd, randomPromotion));
        mainJoinThread.CallDeferred();
	}

	public void GetPerft(int depth)
	{		
		System.Threading.Thread.Sleep(2000);

		var watch = System.Diagnostics.Stopwatch.StartNew();

		GD.Print("Total number of positions: ", Perft(depth).nodes);

		watch.Stop();
		GD.Print(watch.ElapsedMilliseconds / 1000.0, " seconds");

		curCopy.Update();
        mainJoinThread.CallDeferred();
	}

	public PerftCount Perft(int depth)
	{
		PerftCount count = new PerftCount();

		if (depth == 0)
		{
			count.nodes = 1;
			return count;
		}

		Board curB = curCopy.b.Clone();
	
		List<Move> flattenedMoves = curCopy.b.possibleMoves;
		// GD.Print("Depth ", depth, "  ", flattenedMoves.Count);

		foreach (Move move in flattenedMoves)
		{
			curCopy.MakeMove(move);
			System.Threading.Thread.Sleep(g.perftSpeed);

			PerftCount childCount = Perft(depth - 1);
			count.Add(childCount);
			bool isCapture = curCopy.b.capturedPieceN != -1; // only for debugging

			curCopy.UnmakeMove(move, ref curB, ref count);
			System.Threading.Thread.Sleep(g.perftSpeed);

			if (depth == g.perftDepth)
			{
				int pieceN = move.pieceN;
				GD.Print(g.MoveToString(move, isCapture), ": ", childCount.nodes, "   castles: ", childCount.castles, "   en passants: ", childCount.enPassants, "   promotions: ", childCount.promotions);
			}
		}

		return count;
	}

	public void SearchMove()
	{
		int sign = g.sign[botColor];
		MoveScore best = NegaMax(g.botDepth, g.negativeInfinity, g.positiveInfinity, sign);
		curRef.MakeMove(best.move);

		GD.Print(String.Format("Bot Eval: {0}\nTotal Positions: {1}\n", best.score, best.count.nodes));

		System.Threading.Thread.Sleep(g.botSpeed);
        mainJoinThread.CallDeferred();
	}

	public MoveScore NegaMax(int depth, int alpha, int beta, int sign)
	{
		MoveScore best = new MoveScore(new PerftCount(), new Move(), int.MinValue);

		if (depth == 0 || curCopy.b.gameOutcome != -1)
		{
			best.count.nodes = 1;
			best.score = sign * (curCopy.Evaluate());
			return best;
		}

		List<Move> possibleMoves = curCopy.b.possibleMoves;
		// REMINDER: add move ordering
		Board curB = curCopy.b.Clone();

		foreach (Move move in possibleMoves)
		{
			curCopy.MakeMove(move);

			MoveScore movePack = NegaMax(depth - 1, -beta, -alpha, -sign);
			int moveScore = -movePack.score;
			best.count.Add(movePack.count);
			
			if (moveScore >= best.score)
			{
				best.move = move;
				best.score = moveScore;
				// GD.Print(String.Format("Depth: {0}   Move: {1} to {2}", depth - 1, movePack.move.start, movePack.move.end));
			}

			curCopy.UnmakeMove(move, ref curB, ref best.count);
			
			alpha = Math.Max(alpha, moveScore);
			if (alpha >= beta)
			{
				break;
			}
		}

		return best;
	}
}

public struct PerftCount
{
	public int nodes = 0;
	public int castles = 0;
	public int enPassants = 0;
	public int promotions = 0;

	public PerftCount() {}
	public void Add(PerftCount inp)
	{
		nodes += inp.nodes;
		castles += inp.castles;
		enPassants += inp.enPassants;
		promotions += inp.promotions;
	}
}

public struct MoveScore
{
	public PerftCount count;
	public Move move;
	public int score;

	public MoveScore(PerftCount count, Move move, int score)
	{
		this.count = count;
		this.move = move;
		this.score = score;
	}
}