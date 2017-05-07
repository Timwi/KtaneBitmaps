using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Bitmaps;
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

    private int _buttonToPush = 0;
    private bool[][] _bitmap;
    private RuleInfo _triggeredRule;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private static Color[] _lightColors = new[] { new Color(1, .9f, .9f), new Color(.9f, 1, .9f), new Color(.9f, .9f, 1), new Color(1, 1, .9f), new Color(.9f, 1, 1), new Color(1, .9f, 1) };
    private static Color[] _darkColors = new[] { new Color(.75f, .5f, .5f), new Color(.5f, .75f, .5f), new Color(.5f, .5f, .75f), new Color(.75f, .75f, .5f), new Color(.5f, .75f, .75f), new Color(.75f, .5f, .75f) };
    private static string[] _colorNames = new[] { "red", "green", "blue", "yellow", "cyan", "pink" };
    private static int _colorIxCounter = -1;
    private int _colorIx;

    void Start()
    {
        _colorIxCounter = _colorIxCounter == -1 ? Rnd.Range(0, _lightColors.Length) : (_colorIxCounter + 1) % _lightColors.Length;
        _colorIx = _colorIxCounter;

        _moduleId = _moduleIdCounter++;
        Module.OnActivate += ActivateModule;
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

        Bitmap.material.mainTexture = generateTexture();
        Bitmap.material.shader = Shader.Find("Unlit/Transparent");
    }

    private void PushButton(int btn)
    {
        Buttons[btn - 1].AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[btn - 1].transform);
        if (_buttonToPush == 0)
            return;
        Debug.LogFormat("[Bitmaps #{2}] You pushed button #{0}. I expected #{1}.", btn, _buttonToPush, _moduleId);
        if (btn != _buttonToPush)
            Module.HandleStrike();
        else
        {
            Module.HandlePass();
            _buttonToPush = 0;
            Bitmap.gameObject.SetActive(false);
        }
    }

    int[] getQuadrantCounts()
    {
        var qCounts = new int[4];
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                if (_bitmap[x][y])
                    qCounts[(y / 4) * 2 + (x / 4)]++;
        return qCounts;
    }

    private const int quadrantCount = 5;

    sealed class RuleInfo
    {
        public string Name { get; private set; }
        public string Action { get; private set; }
        public Func<bool[][], int> GetAnswer { get; private set; }
        public RuleInfo(string name, string action, Func<bool[][], int> getAnswer)
        {
            Name = name;
            Action = action;
            GetAnswer = getAnswer;
        }
    }

    RuleInfo quadrantCountRule(bool white)
    {
        return new RuleInfo(
            string.Format("Exactly one quadrant has {0} or fewer {1} pixels", quadrantCount, white ? "white" : "black"),
            string.Format("Number of {0} pixels in the other 3 quadrants", white ? "white" : "black"),
            arr =>
            {
                var qCounts = getQuadrantCounts();
                if ((white ? qCounts.Count(sum => sum <= quadrantCount) : qCounts.Count(sum => sum >= (16 - quadrantCount))) != 1)
                    return 0;
                var qIx = (white ? qCounts.IndexOf(sum => sum <= quadrantCount) : qCounts.IndexOf(sum => sum >= (16 - quadrantCount)));
                return (qCounts.Where((sum, ix) => ix != qIx).Sum() + 3) % 4 + 1;
            });
    }

    RuleInfo totalCountRule(int num, bool white)
    {
        return new RuleInfo(
            string.Format("The entire bitmap has {0} or more {1} pixels", num, white ? "white" : "black"),
            string.Format("Number of {0} pixels", white ? "white" : "black"),
            arr =>
            {
                var sum = 0;
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        sum += (arr[x][y] ^ white) ? 0 : 1;
                if (sum >= num)
                    return ((sum + 3) % 4) + 1;
                return 0;
            });
    }

    RuleInfo rowColumnRule = new RuleInfo(
        "Exactly one row or column is completely white or completely black",
        "x- or y-coordinate",
        arr =>
        {
            int answer = 0;
            for (int x = 0; x < 8; x++)
            {
                var isWhite = arr[x][0];
                for (int y = 1; y < 8; y++)
                    if (arr[x][y] != isWhite)
                        goto next;

                if (answer != 0)
                    return 0;
                answer = ((x + 3) % 4) + 1;

                next:;
            }
            for (int y = 0; y < 8; y++)
            {
                var isWhite = arr[0][y];
                for (int x = 1; x < 8; x++)
                    if (arr[x][y] != isWhite)
                        goto next;

                if (answer != 0)
                    return 0;
                answer = ((y + 3) % 4) + 1;

                next:;
            }
            return answer;
        });

    RuleInfo squareRule = new RuleInfo(
        "There is a 3×3 square that is completely white or completely black",
        "x-coordinate of center of first in reading order",
        arr =>
        {
            for (int x = 1; x < 7; x++)
                for (int y = 1; y < 7; y++)
                {
                    var isWhite = arr[x][y];
                    for (int xx = -1; xx < 2; xx++)
                        for (int yy = -1; yy < 2; yy++)
                            if (arr[x + xx][y + yy] != isWhite)
                                goto next;
                    return ((x + 3) % 4) + 1;
                    next:;
                }
            return 0;
        });

    RuleInfo quadrantMajorityRule(string name, string action, Func<int, int, bool> compare, Func<int, int, bool[][], int> getAnswer)
    {
        return new RuleInfo(
            name,
            action,
            arr =>
            {
                var quadrantCounts = new int[4];
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        if (arr[x][y])
                            quadrantCounts[(x / 4) * 2 + (y / 4)]++;
                var w = quadrantCounts.Count(q => q > 8);
                var b = quadrantCounts.Count(q => q < 8);
                return compare(b, w) ? ((getAnswer(b, w, arr) + 3) % 4) + 1 : 0;
            });
    }

    void ActivateModule()
    {
        Debug.LogFormat("[Bitmaps #{0}] Bitmap ({1}):", _moduleId, _colorNames[_colorIx]);
        for (int y = 0; y < 8; y++)
        {
            Debug.LogFormat("[Bitmaps #{0}] {1}", _moduleId, string.Join("", Enumerable.Range(0, 8).Select(x => (_bitmap[x][y] ? "░░" : "██") + (x == 3 ? "│" : null)).ToArray()));
            if (y == 3)
                Debug.LogFormat("[Bitmaps #{0}] ────────┼────────", _moduleId);
        }
        Debug.LogFormat("[Bitmaps #{0}] Quadrant counts: {1}", _moduleId, string.Join(", ", getQuadrantCounts().Select(w => string.Format("{0}w/{1}b", w, 16 - w)).ToArray()));

        var litIndicators = Bomb.GetOnIndicators().Count();
        var unlitIndicators = Bomb.GetOffIndicators().Count();
        var numBatteries = Bomb.GetBatteryCount();
        var numPorts = Bomb.GetPortCount();
        var firstSerialDigit = Bomb.GetSerialNumberNumbers().First();

        var rules = Ut.NewArray(
            quadrantCountRule(true),
            quadrantMajorityRule("There are exactly as many mostly-white quadrants as there are lit indicators", "Number of batteries", (b, w) => w == litIndicators, (b, w, arr) => numBatteries),
            rowColumnRule,
            quadrantMajorityRule("There are fewer mostly-white quadrants than mostly-black quadrants", "Number of mostly-black quadrants", (b, w) => w < b, (b, w, arr) => b),
            totalCountRule(36, true),
            quadrantMajorityRule("There are more mostly-white quadrants than mostly-black quadrants", "Smallest number of black in any quadrant", (b, w) => w > b, (b, w, arr) => getQuadrantCounts().Min()),
            quadrantCountRule(false),
            quadrantMajorityRule("There are exactly as many mostly-black quadrants as there are unlit indicators", "Number of ports", (b, w) => b == unlitIndicators, (b, w, arr) => numPorts),
            squareRule,
            quadrantMajorityRule("There are as many mostly-white quadrants as mostly-black quadrants", "First numeric digit of the serial number", (b, w) => w == b, (b, w, arr) => firstSerialDigit));

        var startRule = Bomb.GetSerialNumberNumbers().Last();
        Debug.LogFormat("[Bitmaps #{0}] Starting rule: {1}", _moduleId, startRule);

        for (int r = 0; r < rules.Length; r++)
        {
            var ruleIndex = (r + startRule) % rules.Length;
            _triggeredRule = rules[ruleIndex];
            _buttonToPush = _triggeredRule.GetAnswer(_bitmap);
            if (_buttonToPush != 0)
            {
                Debug.LogFormat("[Bitmaps #{0}] Applicable rule: {1} = {2}", _moduleId, ruleIndex, _triggeredRule.Name);
                Debug.LogFormat("[Bitmaps #{0}] Answer: {1}", _moduleId, _triggeredRule.Action);
                Debug.LogFormat("[Bitmaps #{0}] Button to push: {1}", _moduleId, _buttonToPush);
                goto found;
            }
        }

        Debug.LogFormat("[Bitmaps #{0}] No applicable rule found. This should never happen.", _moduleId);
        _buttonToPush = 1;

        found:;
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
}
