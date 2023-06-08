using Godot;
using System;

public partial class Chess
{
	public ulong[,] pieces = new ulong[2,6];
	
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
					break;
				}

				int color_n = Char.IsLower(c) ? 1 : 0;
				int piece_n = Array.IndexOf(new char[] {'p', 'n', 'b', 'r', 'q', 'k'}, Char.ToLower(c)); // converts char to int based on piece
				
				pieces[color_n, piece_n] |= 1UL << i;
				i++;
			}
		}
	}
}
