using Godot;

public partial class Customer : Node2D
{
	[Export] public float Speed = 120f;
	[Export] public float Patience = 6f;
	[Export] public int Value = 10;

	// Fired when the customer leaves angry (so Main can fix the queue)
	[Signal] public delegate void LeftAngryEventHandler(Customer who);

	private Vector2 _target;
	private float _patienceLeft;
	private ColorRect _body;

	private enum State { Moving, Waiting, Leaving, Served }
	private State _state = State.Moving;

	// where angry customers run to
	private Vector2 _leaveTarget;

	public override void _Ready()
{
	_body = GetNode<ColorRect>("Body");
	_body.Color = new Color(0.2f, 1f, 1f, 1f); // cyan default (or whatever)
	
	_patienceLeft = Patience;
	_target = GlobalPosition;
	_leaveTarget = new Vector2(-200, GlobalPosition.Y);
}


	public void SetTarget(Vector2 target)
	{
		// If customer is already leaving/served, ignore new targets
		if (_state == State.Leaving || _state == State.Served) return;

		_target = target;
		_state = State.Moving;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		switch (_state)
		{
			case State.Moving:
				GlobalPosition = GlobalPosition.MoveToward(_target, Speed * dt);

				// close enough -> now waiting (patience starts draining)
				if (GlobalPosition.DistanceTo(_target) < 2f)
				{
					GlobalPosition = _target;
					_state = State.Waiting;
				}
				break;

			case State.Waiting:
				_patienceLeft -= dt;
				QueueRedraw(); // update patience bar

				if (_patienceLeft <= 0f)
				{
					BecomeAngry();
				}
				break;

			case State.Leaving:
				GlobalPosition = GlobalPosition.MoveToward(_leaveTarget, (Speed * 1.5f) * dt);

				// once off-screen enough, delete
				if (GlobalPosition.X <= _leaveTarget.X + 2f)
					QueueFree();
				break;
		}
	}

	private void BecomeAngry()
{
	if (_state == State.Leaving || _state == State.Served) return;

	// turn red
	if (IsInstanceValid(_body))
		_body.Color = new Color(1f, 0.2f, 0.2f, 1f);

	_state = State.Leaving;
	EmitSignal(SignalName.LeftAngry, this);
}


	public void Serve()
	{
		if (_state == State.Served || _state == State.Leaving) return;

		_state = State.Served;
		QueueFree();
	}

	public bool IsWaiting() => _state == State.Waiting;

	public override void _Draw()
{
	// Draw a small patience bar above the customer
	float w = 34f;
	float h = 6f;
	Vector2 pos = new Vector2(-w / 2f, -28f);

	float ratio = (Patience <= 0f)
		? 0f
		: Mathf.Clamp(_patienceLeft / Patience, 0f, 1f);

	// Background
	DrawRect(new Rect2(pos, new Vector2(w, h)), new Color(0, 0, 0, 0.6f));

	// Fuck-color logic based on patience
	Color fillColor =
		ratio > 0.6f
			? new Color(0.2f, 1f, 0.2f, 0.9f)   // green = chill
		: ratio > 0.3f
			? new Color(1f, 0.85f, 0.2f, 0.9f)  // yellow = annoyed
			: new Color(1f, 0.2f, 0.2f, 0.95f); // red = FUCK THIS

	// Fill
	DrawRect(new Rect2(pos, new Vector2(w * ratio, h)), fillColor);

	// Border
	DrawRect(
		new Rect2(pos, new Vector2(w, h)),
		new Color(1, 1, 1, 0.6f),
		false,
		1f
	);
}

}
