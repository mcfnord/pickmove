using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using HexC;

namespace BestMove
{
    class Program
    {
        static void Main(string[] args)
        {
            // give me a game id as an arg, and choose a color, and i'll tell you the best move available

     //       string gameId = "freshy"; // just hard code it
            HttpClientSample.Program.TheirMain();
        }
    }
}

namespace HttpClientSample
{
    public class Spot
    {
        public string loc { get; set; }
        public string tok { get; set; }
        public string hue { get; set; }
    }


    public class Move
    {
        public string gameId { get; set; }
        public string color { get; set; }
        public string moveFrom { get; set; }
        public string moveTo { get; set; }
    }

    class Program
    {
        static HttpClient client = new HttpClient();

        /*
        static async Task<List<Spot>> GetProductAsync(string path)
        {

            List<Spot> board = null;
            HttpResponseMessage response = await client.GetAsync(path);
            if (response.IsSuccessStatusCode)
            {
                board = await response.Content.ReadAsAsync<List<Spot>>();
            }
            return board;

        }
        */

        public static void TheirMain()
        {
            RunAsync().GetAwaiter().GetResult();
        }

        static async Task RunAsync()
        {
            // Update port # in the following line.
            client.BaseAddress = new Uri("https://ladybug.international");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                List<Spot> board = null;
                var client = new HttpClient();
                var task = client.GetAsync("https://ladybug.international/Move/Board?gameId=freshy&color=tan")
                  .ContinueWith((taskwithresponse) =>
                  {
                      var response = taskwithresponse.Result;
                      var jsonString = response.Content.ReadAsStringAsync();
                      jsonString.Wait();

                      board = JsonConvert.DeserializeObject<List<Spot>>(jsonString.Result);
                  });
                task.Wait();

                // i have a hybrid POCO. Parse it back into will's format, so i can parse that into my engine format.
                Dictionary<string, string> willBoard = new Dictionary<string, string>();
                foreach (var spot in board)
                {
                    willBoard.Add(spot.loc, spot.tok);
                }

                HexC.Board b = HexC.Program.BoardFromWillsBoard(willBoard);

                // now i got me the board. what i do now? why not ask what the best move for each color is on this board?
                // which is the shallowest analysis first.
                // i believe um this entails examining what each piece can cause,
                // and doing some numeric on me-gains, them-losses.
                // shallow as possible.

                Dictionary<List<PieceEvent>, int> whitesOptions = OptionsExamined(b, HexC.ColorsEnum.White);
                Dictionary<List<PieceEvent>, int> blacksOptions = OptionsExamined(b, HexC.ColorsEnum.Black);
                Dictionary<List<PieceEvent>, int> tansOptions = OptionsExamined(b, HexC.ColorsEnum.Tan);

                // yeah sure maybe later i look derper into what each option set can cause, but right now i wanna numeric of the outcome.
                // and this is fuckin broken cuz i don't think it results in a piece into the portal on successful capture.
                // because i don't care what's off the board...
                // wait, isn't the plan to calculate the captured based on the fielded?
                // yeah, that's the plan.

                var items = from options in whitesOptions
                            orderby options.Value descending
                            select options;

                // just take the top item from items... and exact moves that carry it out.
                // this could get tricky.
                var mahmoove = items.ElementAt(0);

                // we know the event results... but translate that into Moves...
                // but you know i wish i could just um shove this into a special endpoint please.
                // adds and removes in one body.
                var themEvents = mahmoove.Key;

                HttpResponseMessage resp = await client.PostAsJsonAsync("https://ladybug.international/Move/Events", themEvents);
                resp.EnsureSuccessStatusCode();

                /*

                Move move = new Move();
                move.color = "white";
                move.gameId = "freshy";
                move.moveFrom = "n1_p2";
                move.moveTo = "n0_p1";

                HttpResponseMessage resp = await client.PostAsJsonAsync("https://ladybug.international/Move/Pieces", move);
                resp.EnsureSuccessStatusCode();
                */

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            Console.ReadLine();
        }

        static Dictionary<List<PieceEvent>, int> OptionsExamined(HexC.Board b, HexC.ColorsEnum col)
        {
            Dictionary<List<PieceEvent>, int> themDeltas = new Dictionary<List<PieceEvent>, int>();
            foreach (var piece in b.PlacedPiecesThisColor(col))
            {
                var options = b.WhatCanICauseWithDoo(piece);
                foreach (var option in options)
                {
                    int delta = 0;
                    foreach (var anEvent in option)
                    {
                        int swing = 0;
                        switch (anEvent.Regarding.PieceType)
                        {
                            case PiecesEnum.Castle: swing = 5; break;
                            case PiecesEnum.Elephant: swing = 4; break;
                            case PiecesEnum.King: swing = 99; break;
                            case PiecesEnum.Pawn: swing = 1; break;
                            case PiecesEnum.Queen: swing = 20; break;
                        }

                        // if it's a remove, fflip it.
                        if (anEvent.EventType == EventTypeEnum.Remove)
                            swing = -swing;

                        // if it's not me, flip it
                        if (anEvent.Regarding.Color != col)
                            swing = -swing;

                        delta += swing;
                    }
                    themDeltas.Add(option, delta);
                }
            }
            return themDeltas;
        }
    }
}
