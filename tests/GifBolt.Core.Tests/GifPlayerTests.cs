// SPDX-License-Identifier: MIT
using System;
using System.Diagnostics;
using System.IO;

namespace GifBolt.Tests
{
    /// <summary>
    /// Tests unitaires basiques pour GifPlayer via P/Invoke.
    /// </summary>
    public sealed class GifPlayerTests
    {
        public static void Main(string[] args)
        {
            Debug.WriteLine("=== GifBolt.Core P/Invoke Tests ===\n");

            TestPlayerCreation();
            TestInvalidFile();

            Debug.WriteLine("\n=== All tests completed ===");
        }

        private static void TestPlayerCreation()
        {
            Debug.Write("Test: Player creation... ");
            try
            {
                using (var player = new GifPlayer())
                {
                    Debug.WriteLine("✓ PASS");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"✗ FAIL: {ex.Message}");
            }
        }

        private static void TestInvalidFile()
        {
            Debug.Write("Test: Invalid file handling... ");
            try
            {
                using (var player = new GifPlayer())
                {
                    bool result = player.Load("nonexistent.gif");
                    if (!result)
                    {
                        Debug.WriteLine("✓ PASS");
                    }
                    else
                    {
                        Debug.WriteLine("✗ FAIL: Should return false for nonexistent file");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"✗ FAIL: {ex.Message}");
            }
        }
    }
}
