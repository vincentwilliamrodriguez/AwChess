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
	public bool debug = false;
	public bool debug4;
	public bool debug3;

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

			curCopy.UnmakeMove(move, ref curB);
			System.Threading.Thread.Sleep(g.perftSpeed);

			if (depth == g.perftDepth)
			{
				int pieceN = move.pieceN;
				GD.Print(g.MoveToString(move), ": ", childCount.nodes, "   castles: ", childCount.castles, "   en passants: ", childCount.enPassants, "   promotions: ", childCount.promotions);
			}
		}

		return count;
	}

	public void SearchMove()
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		
		MoveScore best = NegaMax(g.botDepth, g.negativeInfinity, g.positiveInfinity);
		watch.Stop();

		int timeDiff = g.botSpeed - (int) watch.ElapsedMilliseconds;
		if (timeDiff > 0)
			System.Threading.Thread.Sleep(timeDiff);

		curRef.MakeMove(best.move);

		GD.Print(String.Format("Bot {2} Eval: {0}\nBest Move: {3}\nTotal Positions: {1}\n", best.score, best.count.nodes, botColor, g.MoveToString(best.move)));
		best.PrintPrincipal();
		GD.Print("\n===============================================================\n");

		g.staticEvaluation = curRef.Evaluate();
        mainJoinThread.CallDeferred();
	}

	public MoveScore NegaMax(int depth, int alpha, int beta)
	{
		MoveScore best = new MoveScore(new PerftCount(), new Move(), g.negativeInfinity);

		if (depth == 0 || curCopy.b.gameOutcome != -1)
		{
			MoveScore qBest = QuiescenceSearch(alpha, beta);
			return qBest;
		}


		List<Move> possibleMoves = curCopy.GetOrderedMoves();
		Board curB = curCopy.b.Clone();

		foreach (Move move in possibleMoves)
		{
			curCopy.MakeMove(move);
			
			MoveScore movePack = NegaMax(depth - 1, -beta, -alpha);
			int moveScore = -movePack.score;
			best.count.Add(movePack.count);
			
			if (moveScore > best.score)
			{
				best.move = move;
				best.score = moveScore;
				best.principal = movePack.principal;
			}

			curCopy.UnmakeMove(move, ref curB);
			
			alpha = Math.Max(alpha, moveScore);
			
			if (alpha >= beta)
			{
				break;
			}
		}

		best.AddMoveToPrincipal();
		best.count.nodes++;
		return best;
	}

	public MoveScore QuiescenceSearch(int alpha, int beta)
	{
		MoveScore best = new MoveScore(new PerftCount(), new Move(-1, -1, -1), 
									   g.sign[curCopy.b.sideToMove] * (curCopy.Evaluate())); // static evaluation
		List<Move> possibleMoves = curCopy.b.possibleMoves;
		List<Move> captureMoves = new List<Move> {};
		Board curB = curCopy.b.Clone();

		foreach (Move move in possibleMoves)
		{
			if (move.capturedPiece != -1)
			{
				captureMoves.Add(move);
			}
		}

		alpha = Math.Max(alpha, best.score);
		if (alpha >= beta || captureMoves.Count == 0 || curCopy.b.gameOutcome != -1)
		{
			best.count.nodes = 1;
			return best;
		}

		captureMoves = curCopy.GetOrderedMoves();
		foreach (Move move in captureMoves)
		{
			curCopy.MakeMove(move);
			
			MoveScore movePack = QuiescenceSearch(-beta, -alpha);
			int moveScore = -movePack.score;
			best.count.Add(movePack.count);
			
			if (moveScore > best.score)
			{
				best.move = move;
				best.score = moveScore;
				best.principal = movePack.principal;
			}

			curCopy.UnmakeMove(move, ref curB);

			alpha = Math.Max(alpha, best.score);
			if (alpha >= beta)
			{
				break;
			}
		}

		best.AddMoveToPrincipal();
		best.count.nodes++;
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
	public Move move;
	public int score;
	public PerftCount count;
	public List<Move> principal = new List<Move> {};

	public MoveScore(PerftCount count, Move move, int score)
	{
		this.count = count;
		this.move = move;
		this.score = score;
	}

	public void AddMoveToPrincipal()
	{
		if (move.start != -1 && move.end != -1)
			principal.Insert(0, move);
	}

	public void PrintPrincipal()
	{
		foreach (Move move in principal)
		{
			GD.Print(g.MoveToString(move));
		}
	}
}


/* NEGAMAX DEBUGGING */
/* 
if (debug && depth == 3)


GD.Print(possibleMoves.Count);

if (depth == 4)
{
	debug4 = (move.start == 8 && move.end == 16);
}

else if (depth == 3)
{
	debug3 = (move.start == 59 && move.end == 31);
}

debug = debug4 && debug3;


if (debug){
	curRef.b = curCopy.b.Copy();
	System.Threading.Thread.Sleep(500);
}

if (debug)
{
	GD.Print(String.Format("\nDEPTH {0}: {1}", depth, g.MoveToString(move)));
	// GD.Print("4 Alpha Beta: ", alpha, ' ', beta);
}



if (debug)
	GD.Print(String.Format("Depth: {0}\t{1}\tPrev: {2}\t\tNew: {3}", depth, g.MoveToString(move), best.score, moveScore));


// GD.Print(String.Format("Depth: {0}   Move: {1} to {2}", depth - 1, movePack.move.start, movePack.move.end));


if (debug){
	curRef.b = curCopy.b.Copy();
	System.Threading.Thread.Sleep(200);
}



if (debug)
	GD.Print(String.Format("Depth: {3}\tMove: {4}\tAlpha Old: {0}\t\tAlpha New: {1}\t\tBeta: {2}", alpha, moveScore, beta, depth, g.MoveToString(move)));
if (debug && depth == 3)
	GD.Print();



if (debug)
{
	GD.Print("Depth ", depth, " ", g.MoveToString(move));
	GD.Print("PRUNE\n");
	// best.PrintPrincipal();

}
 */