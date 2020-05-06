using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bitmaps;
using KModkit;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Bitmaps
/// Created by Timwi, inspired by lumbud84
/// </summary>
public class BitmapsModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public GameObject Screen;
    public Mesh PlaneMesh;
    public KMSelectable[] Buttons;
    public MeshRenderer Bitmap;
    public KMRuleSeedable RuleSeedable;

    private bool[][] _bitmap;
    private bool _isSolved;
    private bool _defaultRuleset;
    private Rule[] _rules;
    private EdgeworkValue _startRule;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private static readonly Color[] _lightColors = new[] { new Color(1, .9f, .9f), new Color(.9f, 1, .9f), new Color(.9f, .9f, 1), new Color(1, 1, .9f), new Color(.9f, 1, 1), new Color(1, .9f, 1) };
    private static readonly Color[] _darkColors = new[] { new Color(.75f, .5f, .5f), new Color(.5f, .75f, .5f), new Color(.5f, .5f, .75f), new Color(.75f, .75f, .5f), new Color(.5f, .75f, .75f), new Color(.75f, .5f, .75f) };
    private static readonly string[] _colorNames = new[] { "red", "green", "blue", "yellow", "cyan", "pink" };

    private static int _colorIxCounter = -1;
    private int _colorIx;

    void Start()
    {
        _colorIxCounter = _colorIxCounter == -1 ? Rnd.Range(0, _lightColors.Length) : (_colorIxCounter + 1) % _lightColors.Length;
        _colorIx = _colorIxCounter;

        _moduleId = _moduleIdCounter++;
        Buttons[0].OnInteract += delegate { PushButton(1); return false; };
        Buttons[1].OnInteract += delegate { PushButton(2); return false; };
        Buttons[2].OnInteract += delegate { PushButton(3); return false; };
        Buttons[3].OnInteract += delegate { PushButton(4); return false; };

        _bitmap = new bool[8][];
        for (int j = 0; j < 8; j++)
        {
            _bitmap[j] = new bool[8];
            for (int i = 0; i < 8; i++)
                _bitmap[j][i] = Rnd.Range(0, 2) == 0;
        }
        _isSolved = false;

        Bitmap.material.mainTexture = generateTexture();
        Bitmap.material.shader = Shader.Find("Unlit/Transparent");

        Debug.LogFormat("[Bitmaps #{0}] Bitmap ({1}):", _moduleId, _colorNames[_colorIx]);
        for (int y = 0; y < 8; y++)
        {
            Debug.LogFormat("[Bitmaps #{0}] {1}", _moduleId, string.Join("", Enumerable.Range(0, 8).Select(x => (_bitmap[x][y] ? "░░" : "██") + (x == 3 ? "│" : null)).ToArray()));
            if (y == 3)
                Debug.LogFormat("[Bitmaps #{0}] ────────┼────────", _moduleId);
        }
        Debug.LogFormat("[Bitmaps #{0}] Quadrant counts: {1}", _moduleId, string.Join(", ", getQuadrantCounts(_bitmap).Select(w => string.Format("{0}w/{1}b", w, 16 - w)).ToArray()));

        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat("[Bitmaps #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);
        if (rnd.Seed == 1)
        {
            _defaultRuleset = true;
            _rules = Ut.NewArray(
                // If exactly one quadrant has 5 or fewer white pixels, the answer is the number of white pixels in the other 3 quadrants.
                new Rule(Condition.QuadrantPixelCount(NumberComparison.ExactlyOne, 5, orFewer: true, white: true), Solution.WhiteInOtherThreeQuadrants),
                // If there are exactly as many mostly-white quadrants as there are lit indicators, the answer is the number of batteries.
                new Rule(Condition.QuadrantMajorityCount(Comparison.Same, white: true, ev: EdgeworkValue.LitIndicators), EdgeworkValue.NumberOfBatteries),
                // If exactly one row or column is completely white or completely black, the answer is its x-/y-coordinate (starting from 1 in the top/left).
                new Rule(Condition.RowOrColumn, Solution.RowColumnCoordinate),
                // If there are fewer mostly-white quadrants than mostly-black quadrants, the answer is the number of mostly-black quadrants.
                new Rule(Condition.QuadrantMajorityComparison(Comparison.Fewer), Solution.NumMostlyBlackQuadrants),
                // If the entire bitmap has 36 or more white pixels, the answer is the total number of white pixels.
                new Rule(Condition.BitmapPixelCount(36, 64, white: true), Solution.NumWhitePixels),
                // If there are more mostly-white quadrants than mostly-black quadrants, the answer is the smallest number of black pixels in any quadrant.
                new Rule(Condition.QuadrantMajorityComparison(Comparison.More), Solution.MinBlackInQuadrant),
                // If exactly one quadrant has 5 or fewer black pixels, the answer is the number of black pixels in the other 3 quadrants.
                new Rule(Condition.QuadrantPixelCount(NumberComparison.ExactlyOne, 5, orFewer: true, white: false), Solution.BlackInOtherThreeQuadrants),
                // If there are exactly as many mostly-black quadrants as there are unlit indicators, the answer is the number of ports.
                new Rule(Condition.QuadrantMajorityCount(Comparison.Same, white: false, ev: EdgeworkValue.UnlitIndicators), EdgeworkValue.Ports),
                // If there is a 3×3 square that is completely white or completely black, the answer is the x-coordinate (starting from 1) of the center of the first such square in reading order.
                new Rule(Condition.Square3, Solution.SquareCoordinate(SquarePoint.Center, 3, y: false, last: false)),
                // If there are exactly as many mostly-white quadrants as mostly-black quadrants, the answer is the first numeric digit of the serial number.
                new Rule(Condition.QuadrantMajorityComparison(Comparison.Same), EdgeworkValue.SnFirstDigit));

            _startRule = EdgeworkValue.SnLastDigit;
        }
        else
        {
            _defaultRuleset = false;

            // Add extra randomness
            var skip = rnd.Next(0, 100);
            for (var i = 0; i < skip; i++)
                rnd.NextDouble();

            // Take a copy of the (static) array
            var edgeworkVariables = rnd.ShuffleFisherYates(EdgeworkValue.Default.ToArray());
            var edgeworkVariableIx = 0;

            // Optional conditions
            var conditions = new List<Condition>
            {
                Condition.RowOrColumn,
                Condition.Row,
                Condition.Column,
                Condition.Square3,
                new[] { Condition.Square2BW, Condition.Square2W, Condition.Square2B }[rnd.Next(0, 3)],
                Condition.QuadrantMajorityCount(Comparison.Same, white: true, ev: edgeworkVariables[edgeworkVariableIx++]),
                Condition.QuadrantMajorityCount(Comparison.More, white: true, ev: edgeworkVariables[edgeworkVariableIx++]),
                Condition.QuadrantMajorityCount(Comparison.Fewer, white: true, ev: edgeworkVariables[edgeworkVariableIx++]),
                Condition.QuadrantMajorityCount(Comparison.Same, white: false, ev: edgeworkVariables[edgeworkVariableIx++]),
                Condition.QuadrantMajorityCount(Comparison.More, white: false, ev: edgeworkVariables[edgeworkVariableIx++]),
                Condition.QuadrantMajorityCount(Comparison.Fewer, white: false, ev: edgeworkVariables[edgeworkVariableIx++])
            };

            // We will make sure that all three conditions from one of these triplets are present so that there is always one rule that matches.
            var pixelCount1 = rnd.Next(3, 8);
            var orFewer = rnd.Next(0, 2) == 0;
            var whiteNotBlack = rnd.Next(0, 2) == 0;
            var pixelCount2 = rnd.Next(26, 30);
            var whiteNotBlack2 = rnd.Next(0, 2) == 0;
            var tripletConditions = Ut.NewArray(
                Ut.NewArray(
                    Condition.QuadrantPixelCount(NumberComparison.None, orFewer ? pixelCount1 : 16 - pixelCount1, orFewer, whiteNotBlack),
                    Condition.QuadrantPixelCount(NumberComparison.ExactlyOne, orFewer ? pixelCount1 : 16 - pixelCount1, orFewer, whiteNotBlack),
                    Condition.QuadrantPixelCount(NumberComparison.MoreThanOne, orFewer ? pixelCount1 : 16 - pixelCount1, orFewer, whiteNotBlack)),
                Ut.NewArray(
                    Condition.QuadrantMajorityComparison(Comparison.Fewer),
                    Condition.QuadrantMajorityComparison(Comparison.More),
                    Condition.QuadrantMajorityComparison(Comparison.Same)),
                Ut.NewArray(
                    Condition.BitmapPixelCount(64 - pixelCount2, 64, whiteNotBlack2),
                    Condition.BitmapPixelCount(0, pixelCount2, whiteNotBlack2),
                    Condition.BitmapPixelCount(pixelCount2 + 1, 63 - pixelCount2, whiteNotBlack2)),
                Ut.NewArray(
                    Condition.QuadrantBalancedNone,
                    Condition.QuadrantBalancedOne,
                    Condition.QuadrantBalancedMoreThanOne));

            var tripletIx = rnd.Next(0, tripletConditions.Length);
            var triplet = tripletConditions[tripletIx];

            for (var i = 0; i < tripletConditions.Length; i++)
                if (i != tripletIx)
                    for (var j = 0; j < tripletConditions[i].Length; j++)
                        conditions.Add(tripletConditions[i][j]);

            rnd.ShuffleFisherYates(conditions);
            conditions.RemoveRange(7, conditions.Count - 7);
            conditions.AddRange(triplet);
            rnd.ShuffleFisherYates(conditions);

            _startRule = edgeworkVariables[edgeworkVariableIx++];

            var solutions = new List<Solution>
            {
                Solution.NumMostlyBlackQuadrants,
                Solution.NumMostlyWhiteQuadrants,
                Solution.NumBalancedQuadrants,
                Solution.NumWhitePixels,
                Solution.NumBlackPixels,
                Solution.MinBlackInQuadrant,
                Solution.MaxBlackInQuadrant,
                Solution.MinWhiteInQuadrant,
                Solution.MaxWhiteInQuadrant
            };

            for (var i = edgeworkVariableIx; i < edgeworkVariables.Length; i++)
                solutions.Add(new Solution(edgeworkVariables[i]));

            var extraSolutions = new Dictionary<Extra, List<Solution>>();

            extraSolutions[Extra.Quadrant] = new List<Solution>
            {
                Solution.WhiteInOtherThreeQuadrants,
                Solution.BlackInOtherThreeQuadrants,
                new Solution("the number of white pixels in the diagonally opposite quadrant", (grid, bomb, extra) => getQuadrantCounts(grid)[3 - extra]),
                new Solution("the number of black pixels in the diagonally opposite quadrant", (grid, bomb, extra) => 16 - getQuadrantCounts(grid)[3 - extra]),
                new Solution("the number of white pixels in the horizontally adjacent quadrant", (grid, bomb, extra) => getQuadrantCounts(grid)[extra ^ 1]),
                new Solution("the number of black pixels in the horizontally adjacent quadrant", (grid, bomb, extra) => 16 - getQuadrantCounts(grid)[extra ^ 1]),
                new Solution("the number of white pixels in the vertically adjacent quadrant", (grid, bomb, extra) => getQuadrantCounts(grid)[extra ^ 2]),
                new Solution("the number of black pixels in the vertically adjacent quadrant", (grid, bomb, extra) => 16 - getQuadrantCounts(grid)[extra ^ 2])
            };
            extraSolutions[Extra.Row] = new List<Solution>
            {
                new Solution("its y-coordinate, counting from 1 from top to bottom", (grid, bomb, extra) => extra + 1),
                new Solution("its y-coordinate, counting from 1 from bottom to top", (grid, bomb, extra) => 8 - extra)
            };
            extraSolutions[Extra.Column] = new List<Solution>
            {
                new Solution("its x-coordinate, counting from 1 from left to right", (grid, bomb, extra) => extra + 1),
                new Solution("its x-coordinate, counting from 1 from right to left", (grid, bomb, extra) => 8 - extra)
            };
            extraSolutions[Extra.Line] = new List<Solution>
            {
                Solution.RowColumnCoordinate,
                new Solution("its x-/y-coordinate, counting from 1 from bottom/right to top/left", (grid, bomb, extra) => 8 - extra)
            };
            extraSolutions[Extra.Square3] = new List<Solution>
            {
                Solution.SquareCoordinate(SquarePoint.Center, 3, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.Center, 3, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.Center, 3, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.Center, 3, y: true, last: true),
                Solution.SquareCoordinate(SquarePoint.TopLeft, 3, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.TopLeft, 3, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.TopLeft, 3, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.TopLeft, 3, y: true, last: true),
                Solution.SquareCoordinate(SquarePoint.TopRight, 3, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.TopRight, 3, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.TopRight, 3, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.TopRight, 3, y: true, last: true),
                Solution.SquareCoordinate(SquarePoint.BottomLeft, 3, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.BottomLeft, 3, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.BottomLeft, 3, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.BottomLeft, 3, y: true, last: true),
                Solution.SquareCoordinate(SquarePoint.BottomRight, 3, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.BottomRight, 3, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.BottomRight, 3, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.BottomRight, 3, y: true, last: true)
            };
            extraSolutions[Extra.Square2] = new List<Solution>
            {
                Solution.SquareCoordinate(SquarePoint.TopLeft, 2, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.TopLeft, 2, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.TopLeft, 2, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.TopLeft, 2, y: true, last: true),
                Solution.SquareCoordinate(SquarePoint.TopRight, 2, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.TopRight, 2, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.TopRight, 2, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.TopRight, 2, y: true, last: true),
                Solution.SquareCoordinate(SquarePoint.BottomLeft, 2, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.BottomLeft, 2, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.BottomLeft, 2, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.BottomLeft, 2, y: true, last: true),
                Solution.SquareCoordinate(SquarePoint.BottomRight, 2, y: false, last: false),
                Solution.SquareCoordinate(SquarePoint.BottomRight, 2, y: true, last: false),
                Solution.SquareCoordinate(SquarePoint.BottomRight, 2, y: false, last: true),
                Solution.SquareCoordinate(SquarePoint.BottomRight, 2, y: true, last: true)
            };

            _rules = new Rule[10];
            for (int i = 0; i < 10; i++)
            {
                var numSol = solutions.Count;
                if (extraSolutions.ContainsKey(conditions[i].Extra))
                    numSol += extraSolutions[conditions[i].Extra].Count;
                var ix = rnd.Next(0, numSol);
                if (ix < solutions.Count)
                {
                    _rules[i] = new Rule(conditions[i], solutions[ix]);
                    solutions.RemoveAt(ix);
                }
                else
                {
                    ix -= solutions.Count;
                    _rules[i] = new Rule(conditions[i], extraSolutions[conditions[i].Extra][ix]);
                    extraSolutions[conditions[i].Extra].RemoveAt(ix);
                }
            }
            Debug.LogFormat("<Bitmaps #{0}> RULES:", _moduleId);
            for (int i = 0; i < 10; i++)
                Debug.LogFormat("<Bitmaps #{0}> {1}, the answer is {2}.", _moduleId, _rules[i].Condition.Name, _rules[i].Solution.Name);
            Debug.LogFormat("[Bitmaps #{0}] SOLUTION AT START (may change if solution depends on number of solved modules):", _moduleId);
        }

        // Evaluate the answer just to log it.
        findAnswer(log: true);
    }

    private int findAnswer(bool log)
    {
        var startRuleIx = _startRule.GetValue(Bomb);
        if (log)
            Debug.LogFormat("[Bitmaps #{0}] Starting rule: {1} = {2}", _moduleId, _startRule.Name, startRuleIx);

        for (int r = 0; r < _rules.Length; r++)
        {
            var ruleIndex = (r + startRuleIx) % _rules.Length;
            var result = _rules[ruleIndex].Condition.Evaluate(_bitmap, Bomb);
            if (result != null)
            {
                var answer = _rules[ruleIndex].Solution.Answer(_bitmap, Bomb, result.Value);
                var btn = (answer + 3) % 4 + 1;
                if (log)
                {
                    Debug.LogFormat("[Bitmaps #{0}] Applicable rule: {1} = {2}", _moduleId, ruleIndex, _rules[ruleIndex].Condition.Name);
                    Debug.LogFormat("[Bitmaps #{0}] Answer: {1} = {2}", _moduleId, _rules[ruleIndex].Solution.Name, answer);
                    Debug.LogFormat("[Bitmaps #{0}] Button to push: {1}", _moduleId, btn);
                }
                return btn;
            }
        }

        Debug.LogFormat("[Bitmaps #{0}] There is a bug in the module. Please contact the author Timwi#0551 on Discord.", _moduleId);
        return 1;
    }

    private void PushButton(int btn)
    {
        Buttons[btn - 1].AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[btn - 1].transform);

        if (_isSolved)
            return;

        // Evaluate the rules again in case they depend on the number of solved modules or the countdown timer.
        if (!_defaultRuleset)
            Debug.LogFormat("[Bitmaps #{0}] You pressed {1} when there were {2} solved modules and the countdown timer was {3:00}:{4:00}.", _moduleId, btn, Bomb.GetSolvedModuleNames().Count, (int) Bomb.GetTime() / 60, (int) Bomb.GetTime() % 60);
        var answer = findAnswer(log: !_defaultRuleset);

        if (answer == btn)
        {
            Debug.LogFormat("[Bitmaps #{0}] You pushed the correct button. Module solved.", _moduleId);
            Module.HandlePass();
            Bitmap.gameObject.SetActive(false);
            _isSolved = true;
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
        }
        else
        {
            Debug.LogFormat("[Bitmaps #{0}] You pushed {1}. Wrong button. Strike.", _moduleId, btn);
            Module.HandleStrike();
        }
    }

    enum Extra
    {
        None,
        Quadrant,
        Line,
        Row,
        Column,
        Square2,
        Square3
    }

    enum Comparison
    {
        Same,
        More,
        Fewer
    }

    enum NumberComparison
    {
        None,
        ExactlyOne,
        MoreThanOne
    }

    enum SquarePoint
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    sealed class EdgeworkValue
    {
        public string Name { get; private set; }
        public Func<KMBombInfo, int> GetValue { get; private set; }
        public EdgeworkValue(string name, Func<KMBombInfo, int> getValue)
        {
            Name = name;
            GetValue = getValue;
        }

        public static readonly EdgeworkValue LitIndicators = new EdgeworkValue("the number of lit indicators", b => b.GetOnIndicators().Count());
        public static readonly EdgeworkValue UnlitIndicators = new EdgeworkValue("the number of unlit indicators", b => b.GetOffIndicators().Count());
        public static readonly EdgeworkValue Ports = new EdgeworkValue("the number of ports", b => b.GetPortCount());
        public static readonly EdgeworkValue NumberOfBatteries = new EdgeworkValue("the number of batteries", b => b.GetBatteryCount());
        public static readonly EdgeworkValue SnFirstDigit = new EdgeworkValue("the first numeric digit of the serial number", b => b.GetSerialNumberNumbers().First());
        public static readonly EdgeworkValue SnLastDigit = new EdgeworkValue("the last numeric digit of the serial number", b => b.GetSerialNumberNumbers().Last());

        public static EdgeworkValue[] Default = Ut.NewArray(
            new EdgeworkValue("the number of indicators", b => b.GetIndicators().Count()),
            LitIndicators,
            UnlitIndicators,
            new EdgeworkValue("the number of indicators with a vowel", b => b.GetIndicators().Count(ind => ind.Any(ch => "AEIOU".Contains(ch)))),
            new EdgeworkValue("the number of indicators with no vowel", b => b.GetIndicators().Count(ind => !ind.Any(ch => "AEIOU".Contains(ch)))),
            Ports,
            new EdgeworkValue("the number of port plates", b => b.GetPortPlateCount()),
            new EdgeworkValue("the number of non-empty port plates", b => b.GetPortPlates().Count(pp => pp.Length > 0)),
            new EdgeworkValue("the number of port types", b => b.GetPorts().Distinct().Count()),
            new EdgeworkValue("the number of port types the bomb has exactly one port of", b => b.GetPorts().GroupBy(p => p).Where(gr => gr.Count() == 1).Count()),
            new EdgeworkValue("the number of duplicate port types", b => b.GetPorts().GroupBy(p => p).Where(gr => gr.Count() > 1).Count()),
            NumberOfBatteries,
            new EdgeworkValue("the number of battery holders", b => b.GetBatteryHolderCount()),
            new EdgeworkValue("the number of AA batteries", b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4)),
            new EdgeworkValue("the number of D batteries", b => b.GetBatteryCount(Battery.D)),
            new EdgeworkValue("the number of letters in the serial number", b => b.GetSerialNumberLetters().Count()),
            new EdgeworkValue("the number of consonants in the serial number", b => b.GetSerialNumberLetters().Count(ch => !"AEIOU".Contains(ch))),
            new EdgeworkValue("the number of vowels in the serial number", b => b.GetSerialNumberLetters().Count(ch => "AEIOU".Contains(ch))),
            new EdgeworkValue("the number of digits in the serial number", b => b.GetSerialNumberNumbers().Count()),
            new EdgeworkValue("the number of odd digits in the serial number", b => b.GetSerialNumberNumbers().Count(n => n % 2 == 1)),
            new EdgeworkValue("the number of even digits in the serial number", b => b.GetSerialNumberNumbers().Count(n => n % 2 == 0)),
            new EdgeworkValue("the number of modules on the bomb (including needies)", b => b.GetModuleNames().Count),
            new EdgeworkValue("the number of non-needy modules on the bomb", b => b.GetSolvableModuleNames().Count),
            new EdgeworkValue("the number of solved modules on the bomb", b => b.GetSolvedModuleNames().Count),
            new EdgeworkValue("the number of unsolved non-needy modules on the bomb", b => b.GetSolvableModuleNames().Count - b.GetSolvedModuleNames().Count),
            SnFirstDigit,
            new EdgeworkValue("the second-last numeric digit of the serial number", b => { var arr = b.GetSerialNumberNumbers().ToArray(); return arr[arr.Length - 2]; }),
            SnLastDigit,
            new EdgeworkValue("the sum of the digits in the serial number", b => b.GetSerialNumberNumbers().Sum()),
            new EdgeworkValue("the ones digit of the seconds in the countdown timer", b => ((int) b.GetTime()) % 10),
            new EdgeworkValue("the tens digit of the seconds in the countdown timer", b => ((int) b.GetTime() / 10) % 10),
            new EdgeworkValue("the ones digit of the minutes in the countdown timer", b => ((int) b.GetTime() / 60) % 10),
            new EdgeworkValue("the tens digit of the minutes in the countdown timer", b => ((int) b.GetTime() / 600) % 10));
    }

    sealed class Condition
    {
        public string Name { get; private set; }
        public Extra Extra { get; private set; }

        // Returns null if the condition doesn’t apply, otherwise returns the Extra value
        public Func<bool[][], KMBombInfo, int?> Evaluate { get; private set; }

        public Condition(string name, Extra extra, Func<bool[][], KMBombInfo, int?> evaluate) { Name = name; Extra = extra; Evaluate = evaluate; }
        public Condition(string name, Extra extra, Func<bool[][], KMBombInfo, bool> evaluate) { Name = name; Extra = extra; Evaluate = (grid, bomb) => { var result = evaluate(grid, bomb); return result ? 0 : (int?) null; }; }

        public static readonly Condition RowOrColumn = new Condition("If exactly one row or column is completely white or completely black", Extra.Line, colRow(true, true));
        public static readonly Condition Row = new Condition("If exactly one row is completely white or completely black", Extra.Row, colRow(false, true));
        public static readonly Condition Column = new Condition("If exactly one column is completely white or completely black", Extra.Column, colRow(true, false));
        public static readonly Condition Square3 = new Condition("If there is a 3×3 square that is completely white or completely black", Extra.Square3, findSquare(3, true, true));
        public static readonly Condition Square2BW = new Condition("If there is a 2×2 square that is completely white or completely black", Extra.Square2, findSquare(2, true, true));
        public static readonly Condition Square2W = new Condition("If there is a 2×2 square that is completely white", Extra.Square2, findSquare(2, true, false));
        public static readonly Condition Square2B = new Condition("If there is a 2×2 square that is completely black", Extra.Square2, findSquare(2, false, true));
        public static readonly Condition QuadrantBalancedNone = new Condition("If no quadrant has 8 white and 8 black pixels", Extra.None, (grid, bomb) => getQuadrantCounts(grid).Count(q => q == 8) == 0);
        public static readonly Condition QuadrantBalancedOne = new Condition("If there is exactly one quadrant with 8 white and 8 black pixels", Extra.Quadrant, (grid, bomb) => getQuadrantCounts(grid).Count(q => q == 8) == 1 ? getQuadrantCounts(grid).IndexOf(q => q == 8) : (int?) null);
        public static readonly Condition QuadrantBalancedMoreThanOne = new Condition("If there is more than one quadrant with 8 white and 8 black pixels", Extra.None, (grid, bomb) => getQuadrantCounts(grid).Count(q => q == 8) > 1);
        public static Condition QuadrantMajorityCount(Comparison comparison, bool white, EdgeworkValue ev)
        {
            return new Condition(
                string.Format("If there are {0} mostly-{1} quadrants {2} {3}", comparison == Comparison.Same ? "exactly as many" : comparison == Comparison.Fewer ? "fewer" : "more", white ? "white" : "black", comparison == Comparison.Same ? "as" : "than", ev.Name),
                Extra.None,
                (grid, bomb) =>
                {
                    var quadrants = getQuadrantCounts(grid);
                    var numMajQuadrants = quadrants.Count(q => white ? (q > 8) : (q < 8));
                    return
                        comparison == Comparison.Same ? (numMajQuadrants == ev.GetValue(bomb)) :
                        comparison == Comparison.More ? (numMajQuadrants > ev.GetValue(bomb)) : (numMajQuadrants < ev.GetValue(bomb));
                });
        }
        public static Condition QuadrantPixelCount(NumberComparison cmp, int amount, bool orFewer, bool white)
        {
            return new Condition(
                string.Format("If {0} quadrant has {1} {2} {3} pixels",
                    cmp == NumberComparison.None ? "no" : cmp == NumberComparison.ExactlyOne ? "exactly one" : "more than one",
                    amount, orFewer ? "or fewer" : "or more", white ? "white" : "black"),
                cmp == NumberComparison.ExactlyOne ? Extra.Quadrant : Extra.None,
                (grid, bomb) =>
                {
                    var matchingQuadrants = getQuadrantCounts(grid).Select(q => white ? (orFewer ? (q <= amount) : (q >= amount)) : (orFewer ? (16 - q <= amount) : (16 - q >= amount))).ToArray();
                    var count = matchingQuadrants.Count(q => q);
                    return cmp == NumberComparison.ExactlyOne
                        ? (count == 1 ? matchingQuadrants.IndexOf(q => q) : (int?) null)
                        : (cmp == NumberComparison.None ? (count == 0) : (count > 1)) ? 0 : (int?) null;
                });
        }
        public static Condition QuadrantMajorityComparison(Comparison cmp)
        {
            return new Condition(
                string.Format("If there are {0} mostly-white quadrants {1} mostly-black quadrants",
                    cmp == Comparison.Fewer ? "fewer" : cmp == Comparison.More ? "more" : "exactly as many",
                    cmp == Comparison.Same ? "as" : "than"),
                Extra.None,
                (grid, bomb) =>
                {
                    var quadrants = getQuadrantCounts(grid);
                    var majWhite = quadrants.Count(q => q > 8);
                    var majBlack = quadrants.Count(q => q < 8);
                    return
                        cmp == Comparison.Fewer ? (majWhite < majBlack) :
                        cmp == Comparison.More ? (majWhite > majBlack) : (majWhite == majBlack);
                });
        }
        public static Condition BitmapPixelCount(int minAmount, int maxAmount, bool white)
        {
            return new Condition(
                string.Format(minAmount == 0 ? "If the entire bitmap has {1} or fewer {2} pixels" : maxAmount == 64 ? "If the entire bitmap has {0} or more {2} pixels" : "If the entire bitmap has between {0} and {1} {2} pixels", minAmount, maxAmount, white ? "white" : "black"),
                Extra.None,
                (grid, bomb) =>
                {
                    var pixels = grid.Sum(row => row.Count(w => w));
                    if (!white)
                        pixels = 64 - pixels;
                    return minAmount <= pixels && pixels <= maxAmount;
                });
        }

        private static Func<bool[][], KMBombInfo, int?> findSquare(int sz, bool checkWhite, bool checkBlack)
        {
            return (grid, bomb) =>
            {
                int? firstInReadingOrder = null;
                int lastInReadingOrder = 0;
                for (int y = 0; y <= 8 - sz; y++)
                    for (int x = 0; x <= 8 - sz; x++)
                    {
                        var isWhite = grid[x][y];
                        if ((isWhite && !checkWhite) || (!isWhite && !checkBlack))
                            continue;
                        for (int xx = 0; xx < sz; xx++)
                            for (int yy = 0; yy < sz; yy++)
                                if (grid[x + xx][y + yy] != isWhite)
                                    goto next;
                        if (firstInReadingOrder == null)
                            firstInReadingOrder = x + 8 * y;
                        lastInReadingOrder = x + 8 * y;
                        next:;
                    }

                // Encode both coordinates in a single integer
                return firstInReadingOrder != null ? firstInReadingOrder.Value * 64 + lastInReadingOrder : (int?) null;
            };
        }

        private static Func<bool[][], KMBombInfo, int?> colRow(bool checkCols, bool checkRows)
        {
            return (grid, bomb) =>
            {
                int? coord = null;
                if (checkCols)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        var isWhite = grid[x][0];
                        for (int y = 1; y < 8; y++)
                            if (grid[x][y] != isWhite)
                                goto next;

                        if (coord != null)
                            // There is more than one such column.
                            return null;

                        coord = x;
                        next:;
                    }
                }
                if (checkRows)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        var isWhite = grid[0][y];
                        for (int x = 1; x < 8; x++)
                            if (grid[x][y] != isWhite)
                                goto next;

                        if (coord != null)
                            // There is more than one such column or row.
                            return null;

                        coord = y;
                        next:;
                    }
                }
                return coord;
            };
        }
    }

    sealed class Solution
    {
        public string Name { get; private set; }
        public Func<bool[][], KMBombInfo, int, int> Answer { get; private set; }
        public Solution(string name, Func<bool[][], KMBombInfo, int, int> answer) { Name = name; Answer = answer; }
        public Solution(EdgeworkValue ev) { Name = ev.Name; Answer = (grid, bomb, extra) => ev.GetValue(bomb); }

        public static readonly Solution WhiteInOtherThreeQuadrants = new Solution("the number of white pixels in the other three quadrants", (grid, bomb, extra) => getQuadrantCounts(grid).Select((q, ix) => ix == extra ? 0 : q).Sum());
        public static readonly Solution BlackInOtherThreeQuadrants = new Solution("the number of black pixels in the other three quadrants", (grid, bomb, extra) => getQuadrantCounts(grid).Select((q, ix) => ix == extra ? 0 : 16 - q).Sum());
        public static readonly Solution RowColumnCoordinate = new Solution("its x-/y-coordinate (starting from 1 in the top/left)", (grid, bomb, extra) => extra + 1);
        public static readonly Solution NumMostlyWhiteQuadrants = new Solution("the number of mostly-white quadrants", (grid, bomb, extra) => getQuadrantCounts(grid).Count(q => q > 8));
        public static readonly Solution NumMostlyBlackQuadrants = new Solution("the number of mostly-black quadrants", (grid, bomb, extra) => getQuadrantCounts(grid).Count(q => q < 8));
        public static readonly Solution NumBalancedQuadrants = new Solution("the number of quadrants with 8 white and 8 black pixels", (grid, bomb, extra) => getQuadrantCounts(grid).Count(q => q == 8));
        public static readonly Solution NumWhitePixels = new Solution("the total number of white pixels", (grid, bomb, extra) => grid.Sum(row => row.Count(p => p)));
        public static readonly Solution NumBlackPixels = new Solution("the total number of black pixels", (grid, bomb, extra) => grid.Sum(row => row.Count(p => !p)));
        public static readonly Solution MinWhiteInQuadrant = new Solution("the smallest number of white pixels in any quadrant", (grid, bomb, extra) => getQuadrantCounts(grid).Min());
        public static readonly Solution MinBlackInQuadrant = new Solution("the smallest number of black pixels in any quadrant", (grid, bomb, extra) => 16 - getQuadrantCounts(grid).Max());
        public static readonly Solution MaxWhiteInQuadrant = new Solution("the largest number of white pixels in any quadrant", (grid, bomb, extra) => getQuadrantCounts(grid).Max());
        public static readonly Solution MaxBlackInQuadrant = new Solution("the largest number of black pixels in any quadrant", (grid, bomb, extra) => 16 - getQuadrantCounts(grid).Min());

        public static Solution SquareCoordinate(SquarePoint pt, int sz, bool y, bool last)
        {
            return new Solution(
                string.Format("the {0}-coordinate (starting from 1) of the {1} of the {2} such square in reading order",
                    y ? "y" : "x",
                    pt == SquarePoint.BottomLeft ? "bottom-left corner" :
                    pt == SquarePoint.BottomRight ? "bottom-right corner" :
                    pt == SquarePoint.TopLeft ? "top-left corner" :
                    pt == SquarePoint.TopRight ? "top-right corner" : "center",
                    last ? "last" : "first"),
                (grid, bomb, extra) => (y ? (last ? extra % 64 : extra / 64) / 8 : (last ? extra % 64 : extra / 64) % 8) + (
                    // +1 on everything because the answer 1-based, while “extra” is 0-based.
                    y && (pt == SquarePoint.BottomLeft || pt == SquarePoint.BottomRight) ? sz :
                    !y && (pt == SquarePoint.TopRight || pt == SquarePoint.BottomRight) ? sz :
                    pt == SquarePoint.Center ? 2 : 1));
        }
    }

    sealed class Rule
    {
        public Condition Condition { get; private set; }
        public Solution Solution { get; private set; }
        public Rule(Condition condition, Solution solution) { Condition = condition; Solution = solution; }
        public Rule(Condition condition, EdgeworkValue ev) { Condition = condition; Solution = new Solution(ev); }
    }

    private static int[] getQuadrantCounts(bool[][] grid)
    {
        var qCounts = new int[4];
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                if (grid[x][y])
                    qCounts[(y / 4) * 2 + (x / 4)]++;
        return qCounts;
    }

    private Texture generateTexture()
    {
        const int padding = 9;
        const int thickSpacing = 6;
        const int thinSpacing = 3;
        const int cellWidth = 30;

        const int bitmapSize = 8 * cellWidth + 6 * thinSpacing + 1 * thickSpacing + 2 * padding;

        var tex = new Texture2D(bitmapSize, bitmapSize, TextureFormat.ARGB32, false);

        for (int x = 0; x < bitmapSize; x++)
            for (int y = 0; y < bitmapSize; y++)
                tex.SetPixel(x, y, new Color(0, 0, 0));

        Action<int, Color[]> drawLine = (int c, Color[] colors) =>
        {
            for (int j = 0; j < bitmapSize; j++)
            {
                tex.SetPixel(c, j, colors[_colorIx]);
                tex.SetPixel(j, c, colors[_colorIx]);
            }
        };

        var offsets = new List<int>();

        var crd = 0;
        for (int p = 0; p < padding; p++)
            drawLine(crd++, _lightColors);
        for (int i = 0; i < 3; i++)
        {
            offsets.Add(crd);
            crd += cellWidth;
            for (int q = 0; q < thinSpacing; q++)
                drawLine(crd++, _lightColors);
        }
        offsets.Add(crd);
        crd += cellWidth;
        for (int q = 0; q < thickSpacing; q++)
            drawLine(crd++, _lightColors);
        for (int i = 0; i < 3; i++)
        {
            offsets.Add(crd);
            crd += cellWidth;
            for (int q = 0; q < thinSpacing; q++)
                drawLine(crd++, _lightColors);
        }
        offsets.Add(crd);
        crd += cellWidth;
        for (int p = 0; p < padding; p++)
            drawLine(crd++, _lightColors);

        for (int x = 0; x < _bitmap.Length; x++)
            for (int y = 0; y < _bitmap[x].Length; y++)
                if (_bitmap[x][y])
                    for (int i = 0; i < cellWidth; i++)
                        for (int j = 0; j < cellWidth; j++)
                            tex.SetPixel(
                                // The bitmap is displayed mirrored in the X direction, so swap left/right here
                                bitmapSize - 1 - offsets[x] - i,
                                offsets[y] + j,
                                _darkColors[_colorIx]);

        tex.Apply();
        return tex;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press 2 [press button 2]";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        switch (command.Trim().Replace("  ", " ").ToLowerInvariant())
        {
            case "press 1": return new[] { Buttons[0] };
            case "press 2": return new[] { Buttons[1] };
            case "press 3": return new[] { Buttons[2] };
            case "press 4": return new[] { Buttons[3] };
        }
        return null;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        Buttons[findAnswer(false) - 1].OnInteract();
        yield break;
    }
}
