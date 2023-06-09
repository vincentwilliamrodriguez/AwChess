using Godot;
using System;
using System.Collections;

public partial class Chess
{
	public ulong[,] pieces = new ulong[2,6];
	public bool sideToMove = true; // true = white, false = black
	public bool[,] castlingRights = new bool[,] {{true, true}, {true, true}};
	public int enPassantSquare = -1; // -1 by default, from 0 to 63 when en passant
	public int halfmoveClock = 0;
	public int fullmoveCounter = 1;
	
	public Chess()
	{
		GD.Print("g awaw");
	}

	public void ImportFromFEN(string FEN)
	{
		// rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1

		string[] fields = FEN.Split(' ');

		/* Piece Placement*/
		string[] ranks = fields[0].Split('/');
		Array.Reverse(ranks); // converting from big endian to little endian

		int i = 0;
		foreach (string rank in ranks)
		{
			foreach (char c in rank)
			{
				if (int.TryParse(c.ToString(), out _)) // checks if c is a number
				{
					i += c - '0'; // converts c from char to int, then adds it to i
					continue;
				}

				int colorN = Char.IsLower(c) ? 1 : 0;
				int pieceN = Array.IndexOf(new char[] {'k', 'q', 'b', 'n', 'r', 'p'}, Char.ToLower(c)); // converts char to int based on piece
				
				pieces[colorN, pieceN] |= 1UL << i;
				i++;
			}
		}

		/* Side to move */
		sideToMove = Convert.ToBoolean(Array.IndexOf(new char[] {'b', 'w'}, char.Parse(fields[1])));

		/* Castling rights */
		var castlingChar = new char[,] {{'K', 'Q'}, {'k', 'q'}};

		for (int colorN = 0; colorN < 2; colorN++) {
			for (int sideN = 0; sideN < 2; sideN++) {

				bool isCastlingAllowed = fields[2].Contains(castlingChar[colorN, sideN]); // checks if color + side is in the castling rights field
				castlingRights[colorN, sideN] = isCastlingAllowed;

			}
		}

		/* En passant target square */
		if (fields[3] != "-") {
			int file = Array.IndexOf(new char[] {'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'}, fields[3][0]);
			int rank = fields[3][1] - '1'; // subtracting 1 because rank counting starts from 0
			enPassantSquare = g.ToIndex(file, rank);
		}

		/* Halfmove Clock */
		halfmoveClock = Convert.ToInt32(fields[4]);

		/* Fullmove counter */
		fullmoveCounter = Convert.ToInt32(fields[5]);


		// GD.Print(String.Format("FEN\nCastling Rights: {4}\nSide to move: {0}\nEn passant: {1}\nHalfmove: {2}\nFullmove: {3}", sideToMove, enPassantSquare, halfmoveClock, fullmoveCounter, castlingRights[0, 0]));
	}
}
