using Godot;
using System.Collections.Generic;

public partial class Main : Node2D
{
	[Export] public PackedScene CustomerScene;
	[Export] public Vector2 SpawnPos = new(100, 400);
	[Export] public Vector2 CounterPos = new(600, 200);
	[Export] public float BaseServiceTime = 3.0f; // seconds to serve 1 customer
	[Export] public float ServerSpeed = 1.0f;     // 0.5 slow, 1 normal, 2 fast

	// Map / progress settings
	[Export] public int TotalDays = 25;
	[Export] public int MoneyGoalForMap = 3000;   // hitting this = 100% map progress
	[Export] public float CpuMoneyPerSecond = 8f; // CPU progress rate
	[Export] public float SecondsPerDay = 45f;    // day length

	private Node2D _customers;

	// Existing UI (kept)
	private Label _moneyLabel;

	// Optional HUD UI (only used if nodes exist)
	private Label _dayLabel;
	private ProgressBar _playerBar;
	private ProgressBar _cpuBar;
	private Label _scoreLabel;

	private float _spawnTimer;

	private Customer _serving;
	private float _serviceProgress;

	private List<Customer> _queue = new();

	// Map totals (progress across the whole map)
	private int _money = 0;        // player total across map
	private float _cpuMoney = 0f;  // cpu total across map

	// Day/round control
	private int _day = 1;
	private float _dayTimer = 0f;

	public override void _Ready()
	{
		_customers = GetNode<Node2D>("Customers");

		// Keep your old label if you still have it
		_moneyLabel = GetNodeOrNull<Label>("MoneyLabel");

		// Optional HUD nodes (won't crash if you don't have them)
		_dayLabel = GetNodeOrNull<Label>("DayLabel");
		_playerBar = GetNodeOrNull<ProgressBar>("PlayerBar");
		_cpuBar = GetNodeOrNull<ProgressBar>("CpuBar");
		_scoreLabel = GetNodeOrNull<Label>("ScoreLabel");

		UpdateHUD();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		_spawnTimer += dt;
		if (_spawnTimer >= 1.2f)
		{
			_spawnTimer = 0f;
			SpawnCustomer();
		}

		UpdateServing(dt);

		// CPU earns across the whole map
		_cpuMoney += dt * CpuMoneyPerSecond;

		// Day timer (days are a round limit, not progress)
		_dayTimer += dt;
		if (_dayTimer >= SecondsPerDay)
		{
			_dayTimer = 0f;
			NextDay();
		}

		CheckMapEnd();
		UpdateHUD();
	}

	// ---- Progress helpers ----
	float PlayerPercent() => Mathf.Clamp((_money / (float)MoneyGoalForMap) * 100f, 0f, 100f);
	float CpuPercent() => Mathf.Clamp((_cpuMoney / MoneyGoalForMap) * 100f, 0f, 100f);

	// ---- Serving logic ----
	void UpdateServing(float dt)
	{
		// If not currently serving, try to start on the first waiting customer
		if (_serving == null || !IsInstanceValid(_serving))
		{
			_serviceProgress = 0f;

			if (_queue.Count == 0) return;

			var first = _queue[0];
			if (IsInstanceValid(first) && first.IsWaiting() && first.GlobalPosition.DistanceTo(CounterPos) < 5f)
			{
				_serving = first;
			}
			else
			{
				_serving = null;
				return;
			}
		}

		// Work on serving over time
		_serviceProgress += dt * ServerSpeed;
		float needed = BaseServiceTime;

		if (_serviceProgress >= needed)
		{
			// finish service (adds to MAP total)
			_money += _serving.Value;
			_serving.Serve();

			_queue.Remove(_serving);
			_serving = null;

			UpdateQueueTargets();
			UpdateHUD();
		}
	}

	// ---- Customer spawning/queue ----
	void SpawnCustomer()
	{
		if (CustomerScene == null) return;

		var c = CustomerScene.Instantiate<Customer>();
		_customers.AddChild(c);
		c.GlobalPosition = SpawnPos;

		c.LeftAngry += OnCustomerLeftAngry;

		_queue.Add(c);
		UpdateQueueTargets();
	}

	void UpdateQueueTargets()
	{
		for (int i = 0; i < _queue.Count; i++)
		{
			if (!IsInstanceValid(_queue[i])) continue;
			_queue[i].SetTarget(CounterPos + new Vector2(0, i * 40));
		}

		_queue.RemoveAll(c => !IsInstanceValid(c));
	}

	void OnCustomerLeftAngry(Customer who)
	{
		_queue.Remove(who);
		UpdateQueueTargets();
	}

	// ---- Day / map end ----
	void NextDay()
	{
		_day++;
		_dayTimer = 0f; // safety reset (even though _Process resets before calling)

		// reset only the "restaurant state" for a new day
		foreach (var child in _customers.GetChildren())
			child.QueueFree();

		_queue.Clear();
		_serving = null;
		_serviceProgress = 0f;
		_spawnTimer = 0f;

		GD.Print($"Day {_day}/{TotalDays}");
	}

	void CheckMapEnd()
	{
		if (PlayerPercent() >= 100f)
		{
			EndMap("You hit 100% first — MAP WIN!");
			return;
		}

		if (CpuPercent() >= 100f)
		{
			EndMap("CPU hit 100% first — MAP LOSS!");
			return;
		}

		// If we advanced past the last day, decide by %
		if (_day > TotalDays)
		{
			if (PlayerPercent() > CpuPercent())
				EndMap("Day limit reached — you win by %!");
			else if (CpuPercent() > PlayerPercent())
				EndMap("Day limit reached — CPU wins by %!");
			else
				EndMap("Day limit reached — tie!");
		}
	}

	void EndMap(string reason)
	{
		GD.Print(reason);

		// Reset everything for now (auto-restart)
		_day = 1;
		_dayTimer = 0f;
		_money = 0;
		_cpuMoney = 0f;

		foreach (var child in _customers.GetChildren())
			child.QueueFree();

		_queue.Clear();
		_serving = null;
		_serviceProgress = 0f;
		_spawnTimer = 0f;

		UpdateHUD();
	}

	// ---- HUD ----
	void UpdateHUD()
	{
		// Old label (kept)
		if (_moneyLabel != null)
			_moneyLabel.Text = $"Money: ${_money}";

		// New optional HUD
		if (_dayLabel != null)
			_dayLabel.Text = $"Day {_day}/{TotalDays}";

		if (_playerBar != null)
			_playerBar.Value = PlayerPercent();

		if (_cpuBar != null)
			_cpuBar.Value = CpuPercent();

		if (_scoreLabel != null)
			_scoreLabel.Text = $"You: ${_money}  CPU: ${Mathf.FloorToInt(_cpuMoney)}";
	}
}
