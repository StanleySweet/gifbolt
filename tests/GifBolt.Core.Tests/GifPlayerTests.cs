// SPDX-License-Identifier: MIT
using System;
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
            Console.WriteLine("=== GifBolt.Core P/Invoke Tests ===\n");

            TestPlayerCreation();
            TestInvalidFile();

            Console.WriteLine("\n=== All tests completed ===");
        }

        private static void TestPlayerCreation()
        {
            Console.Write("Test: Player creation... ");
            try
            {
                using (var player = new GifPlayer())
                {
                    Console.WriteLine("✓ PASS");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAIL: {ex.Message}");
            }
        }

        private static void TestInvalidFile()
        {
            Console.Write("Test: Invalid file handling... ");
            try
            {
                using (var player = new GifPlayer())
                {
                    bool result = player.Load("nonexistent.gif");
                    if (!result)
                    {
                        Console.WriteLine("✓ PASS");
                    }
                    else
                    {
                        Console.WriteLine("✗ FAIL: Should return false for nonexistent file");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAIL: {ex.Message}");
            }
        }
    }
}
