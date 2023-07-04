using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

public partial class AwChess : Node
{
    public Chess curRef;
    public Chess curCopy;
    public Callable mainJoinThread;

    public int botColor;
	public int botEval;
	public Dictionary<ulong, NodeVal> transpositionTable = new Dictionary<ulong, NodeVal> {};
	
	public int IDdepth;
	public NodeVal IDbest;
	public Stopwatch time = new Stopwatch();

	public int count;
	public int count2;

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

		var watch = Stopwatch.StartNew();

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
	{;
		IDbest = new NodeVal(new Move(), g.negativeInfinity);
		IDdepth = 1;
		transpositionTable = new Dictionary<ulong, NodeVal> {};
		count = 0;
		count2 = 0;

		time = Stopwatch.StartNew();
		Board curRefOrig = curRef.b.Copy();
		
		while (time.ElapsedMilliseconds <= g.botMaxID)
		{
			curRef.b = curRefOrig;
			UpdateCopy();
			// count = 0;
			// count2 = 0;

			IDbest = NegaMax(IDdepth, g.negativeInfinity, g.positiveInfinity);
			IDdepth++;

			// GD.Print("Awaw ", IDdepth, " ", g.MoveToString(IDbest.move));
		}

		time.Stop();

		// int timeDiff = g.botSpeed - (int) time.ElapsedMilliseconds;
		// if (timeDiff > 0)
		// 	System.Threading.Thread.Sleep(timeDiff);

		curRef.MakeMove(IDbest.move);
		g.UpdatePiecesDisplay(curRef.b, IDbest.move, botColor);
		g.mainNode.CallDeferred("MovingAnimation");

		GD.Print(String.Format("(Bot {2})\nEval: {0}\nBest Move: {3}\nMax Depth: {5}\nTotal Positions: {1}\nTime: {4} seconds", 
								IDbest.score, 
								count, 
								botColor, 
								g.MoveToString(IDbest.move), 
								time.ElapsedMilliseconds / 1000.0,
								IDdepth)
				);
		
		IDbest.PrintPrincipal();
		GD.Print("\n===============================================================\n");

		g.staticEvaluation = curRef.Evaluate(true);
		botEval = IDbest.score;
        mainJoinThread.CallDeferred();
	}

	public NodeVal NegaMax(int depth, int alpha, int beta)
	{
		count++;
		int alphaOriginal = alpha;
		ulong curKey = curCopy.b.zobristKey;
		NodeVal tTableEntry = TTableLookUp(curKey);

		if (tTableEntry.isValid && tTableEntry.depth >= depth)
		{
			count2++;
			switch (tTableEntry.flag)
			{
				case 1: // EXACT
					return tTableEntry;

				case 2: // LOWERBOUND
					alpha = Math.Max(alpha, tTableEntry.score);
					break;

				case 3: // UPPERBOUND
					beta = Math.Min(beta, tTableEntry.score);
					break;
			}

			if (alpha >= beta)
			{
				return tTableEntry;
			}
		}

		if (depth == 0 || curCopy.b.gameOutcome != -1)
		{
			count--;
			// curRef.b = curCopy.b.Copy();
			// System.Threading.Thread.Sleep(100);

			NodeVal qBest = QuiescenceSearch(alpha, beta);

			// curRef.b = curCopy.b.Copy();
			// System.Threading.Thread.Sleep(100);
			return qBest;
		}

		NodeVal best = new NodeVal(new Move(), g.negativeInfinity);
		List<Move> possibleMoves = GetOrderedMoves();
		Board curB = curCopy.b.Clone();

		foreach (Move move in possibleMoves)
		{
			// if (move.capturedPiece != -1 && move.start == 47)
			// {
			// 	System.Threading.Thread.Sleep(1000);
			// }

			curCopy.MakeMove(move);
			// curRef.b = curCopy.b.Copy();
			// if (move.capturedPiece != -1 && move.start == 47)
			// {
			// 	System.Threading.Thread.Sleep(1000);
			// }
			
			NodeVal movePack = NegaMax(depth - 1, -beta, -alpha);
			int moveScore = -movePack.score;
			
			if (moveScore > best.score)
			{
				best.Adapt(movePack, move);
			}

			curCopy.UnmakeMove(move, ref curB);
			
			alpha = Math.Max(alpha, moveScore);
			
			if (alpha >= beta)
			{
				break;
			}

			if (time.ElapsedMilliseconds > g.botMaxID && 
				depth == IDdepth) // iterative deepening limit when current time exceeds max ID time (on root depth only)
			{
				break;
			}
		}

		best.AddMoveToPrincipal();

		tTableEntry = best; 

		if (best.score <= alphaOriginal)
		{
			tTableEntry.flag = 3; // UPPERBOUND
		}
		else if (best.score >= beta)
		{
			tTableEntry.flag = 2; // LOWERBOUND
		}
		else
		{
			tTableEntry.flag = 1; // EXACT
		}

		tTableEntry.zobristKey = curKey;
		tTableEntry.depth = depth;
		tTableEntry.isValid = true;
		TTableStore(curKey, tTableEntry);

		return best;
	}

	public NodeVal QuiescenceSearch(int alpha, int beta)
	{
		count++;

		NodeVal best = new NodeVal(new Move(-1, -1, -1), 
								   g.sign[curCopy.b.sideToMove] * (curCopy.Evaluate())); // static evaluation
		List<Move> possibleMoves = GetOrderedMoves();
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
			best.AddMoveToPrincipal();
			return best;
		}

		foreach (Move move in captureMoves)
		{
			curCopy.MakeMove(move);
			
			NodeVal movePack = QuiescenceSearch(-beta, -alpha);
			int moveScore = -movePack.score;
			movePack.zobristKey = curCopy.b.zobristKey;
			
			if (moveScore > best.score)
			{
				best.Adapt(movePack, move);
			}

			curCopy.UnmakeMove(move, ref curB);

			alpha = Math.Max(alpha, best.score);
			if (alpha >= beta)
			{
				break;
			}
		}

		best.AddMoveToPrincipal();
		return best;
	}

	
	public List<Move> GetOrderedMoves()
	{
		List<Move> source = curCopy.b.possibleMoves;
		List<int> moveScores = new List<int> {};
		NodeVal tTableEntry = TTableLookUp(curCopy.b.zobristKey);

		foreach (Move move in source)
		{
			int moveScore = 0;

			/* Capture Moves */
			if (move.capturedPiece != -1)
			{
				moveScore += 10 * g.piecesValue[move.capturedPiece]
								- g.piecesValue[move.pieceN];
				
				if (move.end == curCopy.b.lastMove.end) // last moved piece
				{
					moveScore += 1001;
				}
			}

			/* Promotion Moves */
			if (move.promotionPiece != -1)
			{
				moveScore += g.piecesValue[move.promotionPiece];
			}

			/* Transposition Table Hash Move */
			if (tTableEntry.isValid && tTableEntry.move.Equals(move))
			{
				moveScore += 30000;
			}

			moveScores.Add(moveScore);
		}

		List<Move> sortedMoves = source.OrderByDescending(move => moveScores[source.IndexOf(move)]).ToList();
		return sortedMoves;
	}

	public NodeVal TTableLookUp(ulong key)
	{
		ulong address = key & ((1UL << 32) - 1);
		NodeVal node = new NodeVal();

		if (transpositionTable.ContainsKey(address) &&
			key == transpositionTable[address].zobristKey)
		{
			node = transpositionTable[address];
		}
		
		return node;
	}

	public void TTableStore(ulong key, NodeVal node)
	{
		ulong address = key & ((1UL << 32) - 1);
		transpositionTable[address] = node;
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

public struct NodeVal
{
	public Move move;
	public int score;
	public List<Move> principal = new List<Move> {};
	public ulong zobristKey = 0;
	public int depth = -1;
	public int flag = 0;
	public bool isValid = false;

	public NodeVal(Move move, int score)
	{
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

	public void Adapt(NodeVal source, Move prevMove)
	{
		move = prevMove;
		score = -source.score;

		principal = source.principal.Copy();

		zobristKey = source.zobristKey;
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