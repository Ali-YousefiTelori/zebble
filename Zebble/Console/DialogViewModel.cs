﻿using System;
using System.Collections.Generic;
using System.Linq;
using Zebble.Mvvm.AutoUI;
using Olive;

namespace Zebble.Mvvm
{
    partial class DialogViewModel
    {
        string SimulatedTap;

        static void WriteLine(string text = "") => Console.WriteLine(text);
        static void WriteLine(string text, ConsoleColor color) => Console.WriteLine(text, color);

        string DoPrompt(string title, string description)
        {
            DoAlert(title, description);
            System.Console.Write(">");
            return System.Console.ReadLine().Trim();
        }

        void DoShowWaiting(bool block) => WriteLine($"({(block ? "wait" : "loading")}...)", ConsoleColor.DarkGray);

        void DoToast(string message, bool showButton) => WriteLine(message, ConsoleColor.Cyan);

        void DoAlert(string title, string message)
        {
            WriteLine();

            Console.WriteLine("▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄");
            System.Console.WriteLine("█ ");

            if (title.HasValue())
            {
                System.Console.Write("█ ");
                WriteLine(title.ToUpper(), ConsoleColor.White);
                WriteLine("█ ----------------------------------", ConsoleColor.DarkGray);
            }

            foreach (var line in message.Trim().ToLines())
                WriteLine("█ " + line);

            System.Console.WriteLine("█ ");
            Console.WriteLine("▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀");

            WriteLine();
        }

        object DoDecide(string title, string message, Type _, KeyValuePair<string, object>[] buttons)
        {
            DoAlert(title, message);
            object result = null;

            var nodes = buttons.Select((b, i) => new InvokeNode(b.Key, () => result = b.Value, null) { Index = i + 1 }).ToArray();
            nodes.Do(x => x.Render());

            while (true)
            {
                var choice = SimulatedTap.Or(() => Console.AwaitCommand());
                SimulatedTap = null;

                if (choice.IsEmpty()) continue;

                var cmd = nodes.FirstOrDefault(v => v.Index.ToString() == choice);
                if (cmd != null)
                {
                    cmd.Execute();
                    return result;
                }

                Console.ShowError("Invalid choice.");
            }

            throw new Exception("Impossible line");
        }

        /// <summary>
        /// (Autoamted testing only) Call this before calling Dialog.Alert or Dialog.Confirm, to mock the user answer.
        /// </summary>  
        public void WhenPromptedSimulate(string button) => SimulatedTap = button;
    }
}