using Godot;
using System;

public partial class main : Node2D
{

	public override void _Ready()
	{
		var Chess = (Script) GD.Load("res://src/scripts/Chess.cs");
		var current = new Chess();
	}

	public override void _Process(double delta)
	{
		
	}
}
