using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Moments
{
    public class Utils
    {
        public static Texture2D FlipTextureVertically(Texture2D original)
        {
            Texture2D newTex = original;

            var originalPixels = newTex.GetPixels();

            var newPixels = new Color[originalPixels.Length];

            var width = newTex.width;
            var rows = newTex.height;

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < rows; y++)
                {
                    newPixels[x + y * width] = originalPixels[x + (rows - y -1) * width];
                }
            }

            newTex.SetPixels(newPixels);
            newTex.Apply();

            return newTex;
        }

        public static Texture2D FlipTextureHorizontally(Texture2D original)
        {
            Texture2D newTex = original;

            var originalPixels = newTex.GetPixels();

            var newPixels = new Color[originalPixels.Length];

            var width = newTex.width;
            var rows = newTex.height;

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < rows; y++)
                {
                    newPixels[x + y * width] = originalPixels[(width - x - 1) + y * width];
                }
            }

            newTex.SetPixels(newPixels);
            newTex.Apply();

            return newTex;
        }
    }
}