namespace PoolScoreTracker.Domain;

/// <summary>A place at the table: which side, and which of that side's players (always 0 in singles).</summary>
public readonly record struct Seat(Side Side, int Index);

/// <summary>
/// Who is at the table. Sides take turns, and in doubles so do partners, so
/// once the breaker and the player after them are known the whole rotation
/// is: breaker, next, breaker's partner, next's partner, and round again.
///
/// The order only moves when somebody says so - a miss passes the turn, a
/// push handed back steps it back - because nothing here can see the table.
/// Racks move it too: each new rack is broken by the next player in the
/// rotation from the breaking side, so partners take turns to break.
/// </summary>
public sealed class ShootingOrder
{
    /// <summary>The rotation, breaker of the first rack first.</summary>
    public List<Seat> Order { get; } = [];

    /// <summary>Who broke each rack so far, as places in <see cref="Order"/>; the last is this rack's.</summary>
    public List<int> Breakers { get; } = [];

    /// <summary>Who is shooting now, as a place in <see cref="Order"/>.</summary>
    public int Shooter { get; set; }

    public bool IsSet => Order.Count > 0 && Breakers.Count > 0;

    public Seat? Current => IsSet ? Order[Math.Clamp(Shooter, 0, Order.Count - 1)] : null;

    /// <summary>The first rack's breaker and the player after them, as last set.</summary>
    public (Seat Breaker, Seat Next)? Opening => Order.Count >= 2 ? (Order[0], Order[1]) : null;

    /// <summary>Starts the rotation again from this breaker and the player after them.</summary>
    public void Reset(Seat breaker, Seat next, int teamSize)
    {
        Order.Clear();
        for (var i = 0; i < teamSize; i++)
        {
            Order.Add(breaker with { Index = (breaker.Index + i) % teamSize });
            Order.Add(next with { Index = (next.Index + i) % teamSize });
        }

        Breakers.Clear();
        Breakers.Add(0);
        Shooter = 0;
    }

    /// <summary>A miss: the turn passes to the next player in the rotation.</summary>
    public void TurnOver()
    {
        if (IsSet) Shooter = (Shooter + 1) % Order.Count;
    }

    /// <summary>
    /// A push handed back: the other side has put the pusher back in, so the
    /// turn goes back one to them.
    /// </summary>
    public void PushBack()
    {
        if (IsSet) Shooter = (Shooter - 1 + Order.Count) % Order.Count;
    }

    /// <summary>
    /// Brings the racks into line with the score: one breaker per rack started.
    /// A rack added starts the next with the next player in the rotation from
    /// the breaking side; a rack taken off goes back to the one before.
    /// </summary>
    public void FollowRacks(int racksPlayed, bool over, Side? breakingSide)
    {
        if (!IsSet) return;

        var wanted = over ? racksPlayed : racksPlayed + 1;

        while (Breakers.Count > Math.Max(1, wanted))
        {
            Breakers.RemoveAt(Breakers.Count - 1);
            Shooter = Breakers[^1];
        }

        while (Breakers.Count < wanted && breakingSide is { } side)
        {
            var last = Breakers[^1];
            var next = Enumerable.Range(1, Order.Count)
                .Select(step => (last + step) % Order.Count)
                .First(i => Order[i].Side == side);

            Breakers.Add(next);
            Shooter = next;
        }
    }

    public void Clear()
    {
        Order.Clear();
        Breakers.Clear();
        Shooter = 0;
    }
}
