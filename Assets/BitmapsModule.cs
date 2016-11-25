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
/// Created by lumbud84, implemented by Timwi
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
    private int _numTopLeft = 0, _numTopRight = 0, _numBottomLeft = 0, _numBottomRight = 0;

    void Start()
    {
        Debug.Log("[Bitmaps] Started.");
        Module.OnActivate += ActivateModule;
        Buttons[0].OnInteract += delegate { PushButton(1); return false; };
        Buttons[1].OnInteract += delegate { PushButton(2); return false; };
        Buttons[2].OnInteract += delegate { PushButton(3); return false; };
        Buttons[3].OnInteract += delegate { PushButton(4); return false; };

        var bitmap = new bool[8][];
        for (int j = 0; j < 8; j++)
        {
            bitmap[j] = new bool[8];
            for (int i = 0; i < 8; i++)
            {
                var val = Rnd.Range(0, 2) == 0;
                bitmap[j][i] = val;
                if (val)
                {
                    // The bitmap is displayed mirrored in the X direction, so swap left/right here
                    if (j < 4)
                        if (i < 4)
                            _numTopRight++;
                        else
                            _numTopLeft++;
                    else if (i < 4)
                        _numBottomRight++;
                    else
                        _numBottomLeft++;
                }
            }
        }

        Bitmap.material.mainTexture = generateTexture(bitmap);
        Bitmap.material.shader = Shader.Find("Unlit/Transparent");
    }

    private void PushButton(int btn)
    {
        if (_buttonToPush == 0)
            return;
        Debug.LogFormat("[Bitmaps] You pushed button #{0}. I expected #{1}.", btn, _buttonToPush);
        if (btn != _buttonToPush)
            Module.HandleStrike();
        else
        {
            Module.HandlePass();
            _buttonToPush = 0;
            Bitmap.gameObject.SetActive(false);
        }
    }

    void ActivateModule()
    {
        if (_numTopLeft + _numTopRight < 16)
            _buttonToPush = 2;
        else if (_numTopLeft + _numTopRight + _numBottomLeft + _numBottomRight > 32)
            _buttonToPush = 4;
        else if (_numBottomLeft + _numBottomRight > 16)
            _buttonToPush = 1;
        else if (_numTopRight + _numBottomRight <= 16)
            _buttonToPush = 2;
        else if (_numTopLeft > 8)
            _buttonToPush = 3;
        else if (_numTopLeft + _numBottomLeft <= 15)
            _buttonToPush = 1;
        else if (Bomb.GetSerialNumber().Any("AEIOU".Contains))
            _buttonToPush = 3;
        else if (Bomb.GetBatteryCount() >= 3)
            _buttonToPush = 4;
        else
            _buttonToPush = 2;

        Debug.LogFormat("[Bitmaps] Top left={0}, top right={1}, bottom left={2}, bottom right={3}, serial={4}, batteries={5}, button to push={6}", _numTopLeft, _numTopRight, _numBottomLeft, _numBottomRight, Bomb.GetSerialNumber(), Bomb.GetBatteryCount(), _buttonToPush);
    }

    private Texture generateTexture(bool[][] bitmap)
    {
        const int padding = 5;
        const int thickSpacing = 3;
        const int thinSpacing = 1;
        const int cellWidth = 30;

        const int bitmapSize = 8 * cellWidth + 6 * thinSpacing + 1 * thickSpacing + 2 * padding;

        var tex = new Texture2D(bitmapSize, bitmapSize, TextureFormat.ARGB32, false);

        for (int x = 0; x < bitmapSize; x++)
            for (int y = 0; y < bitmapSize; y++)
                tex.SetPixel(x, y, new Color(0, 0, 0, 0));

        Action<int> drawLine = (int c) =>
        {
            for (int j = 0; j < bitmapSize; j++)
            {
                tex.SetPixel(c, j, Color.white);
                tex.SetPixel(j, c, Color.white);
            }
        };

        var offsets = new List<int>();

        var crd = 0;
        for (int p = 0; p < padding; p++)
            drawLine(crd++);
        for (int i = 0; i < 3; i++)
        {
            offsets.Add(crd);
            crd += cellWidth;
            for (int q = 0; q < thinSpacing; q++)
                drawLine(crd++);
        }
        offsets.Add(crd);
        crd += cellWidth;
        for (int q = 0; q < thickSpacing; q++)
            drawLine(crd++);
        for (int i = 0; i < 3; i++)
        {
            offsets.Add(crd);
            crd += cellWidth;
            for (int q = 0; q < thinSpacing; q++)
                drawLine(crd++);
        }
        offsets.Add(crd);
        crd += cellWidth;
        for (int p = 0; p < padding; p++)
            drawLine(crd++);

        for (int y = 0; y < bitmap.Length; y++)
            for (int x = 0; x < bitmap[y].Length; x++)
                if (bitmap[y][x])
                    for (int i = 0; i < cellWidth; i++)
                        for (int j = 0; j < cellWidth; j++)
                            tex.SetPixel(offsets[x] + i, offsets[y] + j, Color.gray);

        tex.Apply();
        return tex;
    }
}
