using System.Globalization;
using TinyKingdom.Units;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static Dictionary<string, string> ReadData(string name)
{
    string path = Path.Combine("Assets", "Data", name + "CombatData.asset");
    var values = new Dictionary<string, string>();
    foreach (string line in File.ReadLines(path))
    {
        string[] parts = line.Trim().Split(':', 2);
        if (parts.Length == 2) values[parts[0]] = parts[1].Trim();
    }
    return values;
}

static SimUnit Unit(Dictionary<string, string> data) => new(
    (UnitType)int.Parse(data["unitType"], CultureInfo.InvariantCulture),
    int.Parse(data["maxHP"], CultureInfo.InvariantCulture),
    int.Parse(data["baseDamage"], CultureInfo.InvariantCulture),
    double.Parse(data["attackInterval"], CultureInfo.InvariantCulture));

static (string winner, double time, int[] blueHP, int[] redHP) Simulate(
    SimUnit[] blue, SimUnit[] red)
{
    for (int iteration = 0; iteration < 10000; iteration++)
    {
        bool blueAlive = blue.Any(unit => unit.HP > 0);
        bool redAlive = red.Any(unit => unit.HP > 0);
        if (!blueAlive || !redAlive)
            return (blueAlive ? "Blue" : redAlive ? "Red" : "Draw", 
                Math.Min(blue.Concat(red).Max(unit => unit.LastAttackTime), 60),
                blue.Select(unit => unit.HP).ToArray(), red.Select(unit => unit.HP).ToArray());

        double time = blue.Concat(red).Where(unit => unit.HP > 0).Min(unit => unit.NextAttackTime);
        Require(time <= 60, "Simulation exceeded 60 seconds.");
        var pending = new Dictionary<SimUnit, int>();
        foreach ((SimUnit[] attackers, SimUnit[] defenders) in new[] { (blue, red), (red, blue) })
        foreach (SimUnit attacker in attackers)
        {
            if (attacker.HP <= 0 || Math.Abs(attacker.NextAttackTime - time) > 0.000001)
                continue;
            SimUnit? target = defenders.FirstOrDefault(unit => unit.HP > 0);
            if (target == null) continue;
            int damage = CombatDamage.Calculate(attacker.Damage, attacker.Type, target.Type);
            pending[target] = pending.GetValueOrDefault(target) + damage;
            attacker.LastAttackTime = time;
            attacker.NextAttackTime += attacker.Interval;
        }
        foreach ((SimUnit target, int damage) in pending)
            target.HP = Math.Max(0, target.HP - damage);
    }
    throw new InvalidOperationException("Simulation iteration limit exceeded.");
}

var warrior = ReadData("Warrior");
var archer = ReadData("Archer");
var lancer = ReadData("Lancer");
Require(warrior["maxHP"] == "260" && warrior["baseDamage"] == "28"
    && warrior["attackRange"] == "1" && warrior["attackInterval"] == "1.1"
    && warrior["moveSpeed"] == "3" && warrior["foodCost"] == "100"
    && warrior["goldCost"] == "80", "Warrior asset differs from GAME_SPEC.md.");
Require(archer["maxHP"] == "60" && archer["baseDamage"] == "10"
    && archer["attackRange"] == "5" && archer["attackInterval"] == "1.2"
    && archer["moveSpeed"] == "3" && archer["foodCost"] == "40"
    && archer["woodCost"] == "40" && archer["attackDelivery"] == "1",
    "Archer asset differs from GAME_SPEC.md.");
Require(lancer["maxHP"] == "125" && lancer["baseDamage"] == "18"
    && lancer["attackRange"] == "1.5" && lancer["attackInterval"] == "1"
    && lancer["moveSpeed"] == "2.8" && lancer["foodCost"] == "60"
    && lancer["goldCost"] == "40", "Lancer asset differs from GAME_SPEC.md.");

Require(CombatDamage.Calculate(28, UnitType.Warrior, UnitType.Archer) == 35,
    "Warrior vs Archer multiplier failed.");
Require(CombatDamage.Calculate(10, UnitType.Archer, UnitType.Warrior) == 8,
    "Archer vs Warrior multiplier failed.");
Require(CombatDamage.Calculate(10, UnitType.Archer, UnitType.Lancer) == 13,
    "Archer vs Lancer multiplier failed.");
Require(CombatDamage.Calculate(18, UnitType.Lancer, UnitType.Archer) == 14,
    "Lancer vs Archer multiplier failed.");
Require(CombatDamage.Calculate(18, UnitType.Lancer, UnitType.Warrior) == 27,
    "Lancer vs Warrior multiplier failed.");
Require(CombatDamage.Calculate(28, UnitType.Warrior, UnitType.Lancer) == 28,
    "Warrior vs Lancer multiplier failed.");
Require(CombatDamage.Calculate(28, UnitType.Warrior, UnitType.Warrior) == 28,
    "Neutral multiplier failed.");

var duel = Simulate(new[] { Unit(warrior) }, new[] { Unit(lancer) });
Require(duel.winner == "Blue", "Balance mismatch: one Warrior did not beat one Lancer.");
Console.WriteLine($"A Warrior vs Lancer: {duel.winner} wins; HP {duel.blueHP[0]} vs {duel.redHP[0]}");

var pair = Simulate(new[] { Unit(warrior) }, new[] { Unit(lancer), Unit(lancer) });
Require(pair.winner == "Red", "Balance mismatch: two Lancers did not beat one Warrior.");
Console.WriteLine($"B Warrior vs two Lancers: {pair.winner} wins; HP {pair.blueHP[0]} vs {string.Join(',', pair.redHP)}");

var closeArcher = Simulate(new[] { Unit(warrior) }, new[] { Unit(archer) });
Require(closeArcher.winner == "Blue", "Warrior did not beat Archer at melee range.");
Console.WriteLine($"C Warrior vs Archer at melee range: {closeArcher.winner} wins; HP {closeArcher.blueHP[0]} vs {closeArcher.redHP[0]}");

Console.WriteLine("D Archer vs Lancer: x1.25, 10 base damage -> 13 HP damage per projectile hit");
Console.WriteLine("E Lancer vs Warrior: x1.50, 18 base damage -> 27 HP damage");
Console.WriteLine("F Warrior vs Lancer: x1.00, 28 base damage -> 28 HP damage");
Console.WriteLine("Deterministic counter and matchup validation PASS.");

sealed class SimUnit
{
    public SimUnit(UnitType type, int hp, int damage, double interval)
    {
        Type = type;
        HP = hp;
        Damage = damage;
        Interval = interval;
    }
    public UnitType Type { get; }
    public int HP { get; set; }
    public int Damage { get; }
    public double Interval { get; }
    public double NextAttackTime { get; set; }
    public double LastAttackTime { get; set; }
}
