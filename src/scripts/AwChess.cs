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

		Random random = new Random();
		int randomMove = random.Next(flattenedMoves.Count);
		int randomPiece = flattenedMoves[randomMove].pieceN;
		int randomStart = flattenedMoves[randomMove].start;
		int randomEnd = flattenedMoves[randomMove].end;
		int randomPromotion = g.promotionPieces[random.Next(4)];

        System.Threading.Thread.Sleep(50);

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
			int pieceN = move.pieceN;

			curCopy.MakeMove(move);
			System.Threading.Thread.Sleep(g.perftSpeed);

			PerftCount childCount = Perft(depth - 1);
			count.Add(childCount);
			bool isCapture = curCopy.b.capturedPieceN != -1; // only for debugging

			curCopy.UnmakeMove(move, ref curB, ref count);
			System.Threading.Thread.Sleep(g.perftSpeed);

			if (depth == g.perftDepth)
			{
				GD.Print(g.MoveToString(move, isCapture), ": ", childCount.nodes, "   castles: ", childCount.castles, "   en passants: ", childCount.enPassants, "   promotions: ", childCount.promotions);
			}
		}

		return count;
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