using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using Godot.Collections;

namespace GodotGameJamGame.Scripts;

public enum DirectionKey
{
	None = -1, Up = 1, Down, Left, Right
}

public enum TileValue
{
	Empty = 0, Wall, Move, Drop, PickUp, Portal, Start, End
}

public partial class Player : AnimatedSprite2D
{
	private Global global;

	private static readonly string _className = "Player";
	
	[Export] private Array<NodePath> _tileMapPaths;
	[Export] private NodePath _barPath;

	[Export] private NodePath _upRedPath;
	[Export] private NodePath _upBluePath;
	
	[Export] private NodePath _downRedPath;
	[Export] private NodePath _downBluePath;
	
	[Export] private NodePath _leftRedPath;
	[Export] private NodePath _leftBluePath;
	
	[Export] private NodePath _rightRedPath;
	[Export] private NodePath _rightBluePath;

	[Export] private Texture2D _arrowTex;

	private Sprite2D _upRed;
	private Sprite2D _upBlue;
	
	private Sprite2D _downRed;
	private Sprite2D _downBlue;
	
	private Sprite2D _leftRed;
	private Sprite2D _leftBlue;
	
	private Sprite2D _rightRed;
	private Sprite2D _rightBlue;

	private HashSet<DirectionKey> _enabledKeys = new();

	private TileMap _tileMap;
	private Sprite2D _bar;
	private bool _isMoving;
	private Timer _timer;
	
	private static readonly Vector2I Size = new (36, 36);
	private static readonly Vector2I HalfSize = new (18, 18);

	private const string IdLayer = "id";
	private const string KeyLayer = "key";
	
	private Vector2I _playerPos;

	private Godot.Collections.Dictionary<Vector2I, Sprite2D> arrows = new ();

	private void SetPath(NodePath path, ref Sprite2D sprite)
	{
		if (path != null && path.ToString() != "")
		{
			sprite = GetNode<Sprite2D>(path);
		}
		else
		{
			Debug.Fail(_className + ": path not set, check inspector.");
		}
	}

	private void RemoveKey(DirectionKey key)
	{
		_enabledKeys.Remove(key);
		switch (key)
		{
			case DirectionKey.Up:
				_upBlue.Visible = false;
				_upRed.Visible = true;
				break;

			case DirectionKey.Down:
				_downBlue.Visible = false;
				_downRed.Visible = true;
				break;

			case DirectionKey.Left:
				_leftBlue.Visible = false;
				_leftRed.Visible = true;
				break;

			case DirectionKey.Right:
				_rightBlue.Visible = false;
				_rightRed.Visible = true;
				break;

			case DirectionKey.None:
			default:
				GD.Print("Invalid direction key");
				break;
		}
	}
	
	private void AddKey(DirectionKey key)
	{
		_enabledKeys.Add(key);
		switch (key)
		{
			case DirectionKey.Up:
				_upBlue.Visible = true;
				_upRed.Visible = false;
				break;

			case DirectionKey.Down:
				_downBlue.Visible = true;
				_downRed.Visible = false;
				break;

			case DirectionKey.Left:
				_leftBlue.Visible = true;
				_leftRed.Visible = false;
				break;

			case DirectionKey.Right:
				_rightBlue.Visible = true;
				_rightRed.Visible = false;
				break;

			case DirectionKey.None:
			default:
				GD.Print("Invalid direction key");
				break;
		}
	}

	private void AddArrow(Vector2I tilePos, DirectionKey key)
	{
		Sprite2D arrow = new Sprite2D();
		arrow.Texture = _arrowTex;
		arrow.Rotation = key switch
		{
			DirectionKey.Down => Mathf.Pi,
			DirectionKey.Left => -Mathf.Pi / 2,
			DirectionKey.Right => Mathf.Pi / 2,
			_ => arrow.Rotation
		};
		
		GetParent().AddChild(arrow);

		arrow.Position = Position + HalfSize;
		
		arrows.Add(tilePos, arrow);
	}
	
	private void RemoveArrow(Vector2I tilePos)
	{
		if (arrows.ContainsKey(tilePos))
		{
			var arrow = arrows[tilePos];
			arrows.Remove(tilePos);
			arrow.QueueFree();
		}
		else
		{
			Debug.Fail("No arrow to remove, check RemoveArrow()");
		}
	}

	private void CheckLevels()
	{
		for (int i = 0; i < _tileMapPaths.Count; ++i)
		{
			if (_tileMapPaths[i] != null && _tileMapPaths[i].ToString() != "")
			{
				GD.Print("DONE: " + i);
			}
			else
			{
				Debug.Fail("TileMapPath path not set, check inspector.");
			}
		}
		
		_tileMap = GetNode<TileMap>(_tileMapPaths[global.levelIndex]);
		_tileMap.SetProcess(true);
		_tileMap.Visible = true;
		_playerPos = _tileMap.GetMeta("playerstart").As<Vector2I>();
	}

	public override void _Ready()
	{
		global = (Global)GetNode("/root/Global");

		CheckLevels();

		if (GetCellData(_playerPos.X, _playerPos.Y, IdLayer) != (int)TileValue.Start)
		{
			Debug.Fail("Start Position not valid, check inspector and set tile pos.");
		}
		else
		{
			_tileMap.SetCell(1, _playerPos);
			Position = Size * _playerPos;
		}
		
		if (_barPath != null && _barPath.ToString() != "")
		{
			_bar = GetNode<Sprite2D>(_barPath);
		}
		else
		{
			Debug.Fail("BarSprite path not set, check inspector.");
		}

		_timer = GetChild<Timer>(0);
		_timer.Timeout += OnTimerTimeout;

		Play();

		_enabledKeys.Add(DirectionKey.Up);
		_enabledKeys.Add(DirectionKey.Down);
		_enabledKeys.Add(DirectionKey.Right);
		_enabledKeys.Add(DirectionKey.Left);
		
		SetPath(_leftRedPath, ref _leftRed);
		SetPath(_leftBluePath, ref _leftBlue);

		SetPath(_rightRedPath, ref _rightRed);
		SetPath(_rightBluePath, ref _rightBlue);

		SetPath(_upRedPath, ref _upRed);
		SetPath(_upBluePath, ref _upBlue);
		
		SetPath(_downRedPath, ref _downRed);
		SetPath(_downBluePath, ref _downBlue);
		
		
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isMoving)
		{
			return;
		}
		
		var direction = new Vector2I(0, 0);
		
		if (_enabledKeys.Contains(DirectionKey.Up) && Input.IsActionJustPressed("ui_up"))
		{
			SetTurn(ref direction, DirectionKey.Up, 0, -1);
		}
		if (_enabledKeys.Contains(DirectionKey.Down) && Input.IsActionJustPressed("ui_down"))
		{
			SetTurn(ref direction, DirectionKey.Down, 0, 1);
		}
		if (_enabledKeys.Contains(DirectionKey.Left) && Input.IsActionJustPressed("ui_left"))
		{
			SetTurn(ref direction, DirectionKey.Left, -1, 0);
		}
		if (_enabledKeys.Contains(DirectionKey.Right) && Input.IsActionJustPressed("ui_right"))
		{
			SetTurn(ref direction, DirectionKey.Right, 1, 0);
		}

		if (direction == new Vector2I(0, 0)) return;
		_playerPos += direction;
		Position = Size * _playerPos;
		_isMoving = true;
			
		_timer.Start();
	}

	private void OnTimerTimeout()
	{
		_isMoving = false;
	}
	
	private int GetCellData(int posX, int posY, string layerName)
	{
		var nullVal = (layerName == "id") ? 0 : -1;
		var tileData = _tileMap.GetCellTileData(1, new Vector2I(posX, posY));
		return tileData != null ? tileData.GetCustomData(layerName).As<int>() : nullVal;
	}

	private void PrintCellTileData(Vector2I tilePos, string stuff)
	{
		GD.Print(stuff + ", Cell" + tilePos + " : " + Enum.GetName((TileValue)GetCellData(_playerPos.X, _playerPos.Y, IdLayer)));
	}
	
	private void PrintCellKeyData(Vector2I tilePos, string stuff)
	{
		GD.Print(stuff + ", Cell" + tilePos + " : " + Enum.GetName((DirectionKey)GetCellData(_playerPos.X, _playerPos.Y, KeyLayer)));
	}

	private void SetTurn(ref Vector2I direction, DirectionKey key, int x, int y)
	{
		// check current cell data
		switch ((TileValue)GetCellData(_playerPos.X, _playerPos.Y, IdLayer))
		{
			case TileValue.Drop:
				RemoveKey(key);
				
				_tileMap.SetCell(1, _playerPos, 7, new Vector2I(1, 0), 5);
				_tileMap.GetCellTileData(1, _playerPos).SetCustomData(KeyLayer, (int)key);

				AddArrow(_playerPos, key);

				break;
			case TileValue.PickUp:
				AddKey((DirectionKey)GetCellData(_playerPos.X, _playerPos.Y, KeyLayer));
				
				_tileMap.SetCell(1, _playerPos, 7, new Vector2I(1, 0), 3);
				
				RemoveArrow(_playerPos);

				break;
		}

		// check the cell data
		switch ((TileValue)GetCellData(_playerPos.X + x, _playerPos.Y + y, IdLayer))
		{
			case TileValue.Wall:
				break;
			case TileValue.Move:
				switch ((TileValue)GetCellData(_playerPos.X + 2 * x, _playerPos.Y + 2 * y, IdLayer))
				{
					case TileValue.Wall:
						direction.Y += y;
						direction.X += x;
						break;
					default: 
						direction.Y += 2 * y;
						direction.X += 2 * x;
						break;
				}
				break;
			case TileValue.End:
				if (CheckEndCondition(_tileMap.GetMeta("end").As<Godot.Collections.Array<int>>()))
				{
					global.levelIndex += 1;
					
					_tileMap.SetProcess(false);
					_tileMap.Visible = false;

					GetTree().ReloadCurrentScene();

					/*
					_tileMap = GetNode<TileMap>(_tileMapPaths[levelIndex]);
					
					_tileMap.SetProcess(true);
					_tileMap.Visible = true;
					
					_playerPos = _tileMap.GetMeta("playerstart").As<Vector2I>();
					*/
				}
				direction.Y += y;
				direction.X += x;
				break;
			default:
				direction.Y += y;
				direction.X += x;
				break;
		}
	}
	
	private bool CheckEndCondition(Godot.Collections.Array<int> data)
	{
		// Convert the data array to a HashSet
		HashSet<DirectionKey> dataSet = new HashSet<DirectionKey>();
		foreach (int item in data)
		{
			if (Enum.IsDefined(typeof(DirectionKey), item))
			{
				dataSet.Add((DirectionKey)item);
			}
			else
			{
				GD.Print($"Invalid value {item} in data");
				return false;
			}
		}

		// Compare the two sets
		return dataSet.SetEquals(_enabledKeys);
	}
}
