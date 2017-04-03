using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace RockMusicChecker
{
    class Program
    {
        private static Timer _timer;

        public static void Main()
        {

            _timer = new Timer(timer_Elapsed);
            _timer.Change(0, 60000);
            Console.ReadKey();
        }

        private static void timer_Elapsed(object state) => CallTheRock();

        public static string emailAddress = "";
        public static string emailPassword = "";
        public static string emailToAddress = "";
        public static string sqlString = "";

        public static bool CallTheRock()
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://www.therock.net.nz/Portals/0/Inbound/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

                HttpResponseMessage rockResponse = client.GetAsync("NowPlaying/NowPlaying.aspx").Result;
                if (rockResponse.IsSuccessStatusCode)
                {
                    XmlSerializer serializer = new XmlSerializer(typeof (nexgen_audio_export));

                    string xml = rockResponse.Content.ReadAsStringAsync().Result;

                    using (TextReader reader = new StringReader(xml))
                    {
                        nexgen_audio_export result = (nexgen_audio_export) serializer.Deserialize(reader);
                        
                        CheckDb(result);
                        CheckIfPlayedTwice();
                        //CheckSongOfDay("Never Tear Us Apart");

                    }
                    return true;
                }
                Console.WriteLine("Could Not Connect to The Rock");
                return false;
            }
        }

        public static void CheckSongOfDay(string SongOfDay)
        {
            using (SqlConnection conn = new SqlConnection())
            {
                conn.ConnectionString = sqlString;
                conn.Open();

                //Check if the row exists in the database
                SqlCommand cmdCheckSongOfDay =
                    new SqlCommand(
                        "SELECT Playing_Next_Title FROM Songs WHERE Playing_Next_Title = @Playing_Next_Title AND WorkDay IS NULL AND Emailed IS NULL");
                cmdCheckSongOfDay.CommandType = CommandType.Text;
                cmdCheckSongOfDay.Connection = conn;
                cmdCheckSongOfDay.Parameters.AddWithValue("@Playing_Next_Title", SongOfDay);
                cmdCheckSongOfDay.ExecuteNonQuery();

                using (SqlDataReader sqlReader = cmdCheckSongOfDay.ExecuteReader())
                {
                    //If row doesnt exist, Insert
                    if (sqlReader.Read())
                    {
                        Console.WriteLine(" ");
                        Console.WriteLine("**********************");
                        Console.WriteLine("SONG OF DAY COMING UP");
                        Console.WriteLine("**********************");
                        Console.WriteLine(" ");

                        try
                        {
                            SmtpClient client = new SmtpClient
                            {
                                Host = "smtp.gmail.com",
                                Port = 587,
                                EnableSsl = true,
                                DeliveryMethod = SmtpDeliveryMethod.Network,
                                Credentials = new System.Net.NetworkCredential(emailAddress, emailPassword),
                                Timeout = 10000,
                            };
                            MailMessage mm = new MailMessage(emailAddress, emailToAddress, "Song of the day coming up!", "SHIVVERS BRO, THE SONG OF THE DAY IS PLAYING NEXT");
                            client.Send(mm);
                            conn.Close();
                            using (SqlConnection connec = new SqlConnection())
                            {
                                connec.ConnectionString = sqlString;
                                connec.Open();
                                //Update DB to say email was sent
                                SqlCommand updateDbCommand =
                                    new SqlCommand(
                                        "UPDATE Songs SET Emailed = @Emailed WHERE Playing_Next_Title = @Playing_Next_Title");
                                updateDbCommand.CommandType = CommandType.Text;
                                updateDbCommand.Parameters.AddWithValue("@Playing_Next_Title", SongOfDay);
                                updateDbCommand.Parameters.AddWithValue("@Emailed", "TRUE");
                                updateDbCommand.Connection = connec;
                                updateDbCommand.ExecuteNonQuery();
                                connec.Close();
                            }

                            Console.WriteLine("Email Sent");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Could not end email\n\n" + e.ToString());
                        }
                    }
                }
            }
        }

        public static void CheckDb(nexgen_audio_export result)
        {
            using (SqlConnection conn = new SqlConnection())
            {
                conn.ConnectionString = sqlString;
                conn.Open();

                //Check if the row exists in the database
                SqlCommand cmdCheck =
                    new SqlCommand(
                        "SELECT Now_Playing_Title, Playing_Next_Title FROM Songs WHERE Now_Playing_Title = @Now_Playing_Title AND Playing_Next_Title = @Playing_Next_Title AND WorkDay IS NULL");
                cmdCheck.CommandType = CommandType.Text;
                cmdCheck.Connection = conn;
                cmdCheck.Parameters.AddWithValue("@Now_Playing_Title", result.audio[0].title);
                cmdCheck.Parameters.AddWithValue("@Playing_Next_Title", result.audio[1].title);
                cmdCheck.ExecuteNonQuery();

                using (SqlDataReader sqlReader = cmdCheck.ExecuteReader())
                {
                    //If row doesnt exist, Insert
                    if (!sqlReader.Read())
                    {
                        conn.Close();
                        
                        Console.Write("Now playing ");
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(result.audio[0].title);
                        Console.ResetColor();
                        Console.Write(" by ");
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(result.audio[0].artist);
                        Console.ResetColor();
                        Console.WriteLine("");

                        Console.Write("Playing Next ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(result.audio[1].title);
                        Console.ResetColor();
                        Console.Write(" by ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(result.audio[1].artist);
                        Console.ResetColor();
                        Console.WriteLine("");
                        Console.WriteLine("");


                        conn.ConnectionString = sqlString;
                        conn.Open();
                        //Insert Into Database
                        SqlCommand cmd =
                            new SqlCommand(
                                "INSERT INTO Songs (Now_Playing_Title, Now_Playing_Artist, Playing_Next_Title, Playing_Next_Artist, Time) VALUES (@Now_Playing_Title, @Now_Playing_Artist, @Playing_Next_Title, @Playing_Next_Artist, @Time)");
                        cmd.CommandType = CommandType.Text;
                        cmd.Connection = conn;
                        cmd.Parameters.AddWithValue("@Now_Playing_Title", result.audio[0].title);
                        cmd.Parameters.AddWithValue("@Now_Playing_Artist", result.audio[0].artist);
                        cmd.Parameters.AddWithValue("@Playing_Next_Title", result.audio[1].title);
                        cmd.Parameters.AddWithValue("@Playing_Next_Artist", result.audio[1].artist);
                        cmd.Parameters.AddWithValue("@Time", DateTime.Now.ToString());

                        cmd.ExecuteNonQuery();
                        conn.Close();
                    }

                }
            }
        }

        public static void CheckIfPlayedTwice()
        {
            using (SqlConnection conn = new SqlConnection())
            {
                conn.ConnectionString = sqlString;
                conn.Open();
                
                SqlCommand cmdCheckPlaying =
                    new SqlCommand("SELECT Now_Playing_Title, Now_Playing_Artist, Time FROM Songs WHERE Now_Playing_Title IN(SELECT Now_Playing_Title FROM Songs WHERE WorkDay IS NULL AND Emailed IS NULL GROUP BY Now_Playing_Title HAVING COUNT(Now_Playing_Title)>1)AND WorkDay IS NULL AND Emailed IS NULL");
                cmdCheckPlaying.CommandType = CommandType.Text;
                cmdCheckPlaying.Connection = conn;
                cmdCheckPlaying.ExecuteNonQuery();

                using (SqlDataReader sqlReaderCheck = cmdCheckPlaying.ExecuteReader())
                {
                    //TWO SONGS PLAYED TODAY
                    if (sqlReaderCheck.Read())
                    {
                        Console.WriteLine(" ");
                        Console.WriteLine("***************************************");
                        Console.WriteLine(sqlReaderCheck["Now_Playing_Title"] + " PLAYED TWICE TODAY!");
                        Console.WriteLine("***************************************");
                        Console.WriteLine(" ");

                        try
                        {
                            SmtpClient client = new SmtpClient
                            {
                                Host = "smtp.gmail.com",
                                Port = 587,
                                EnableSsl = true,
                                DeliveryMethod = SmtpDeliveryMethod.Network,
                                Credentials = new System.Net.NetworkCredential(emailAddress, emailPassword),
                                Timeout = 10000,
                            };
                            MailMessage mm = new MailMessage(emailAddress, emailToAddress, "Two songs played today!", "SHIVVERS BRO, " + sqlReaderCheck["Now_Playing_Title"] +" PLAYED TWICE TODAY");
                            client.Send(mm);

                            using (SqlConnection connec = new SqlConnection())
                            {
                                connec.ConnectionString = sqlString;
                                connec.Open();
                                //Update DB to say email was sent
                                SqlCommand updateDbCommand =
                                    new SqlCommand(
                                        "UPDATE Songs SET Emailed = @Emailed WHERE Now_Playing_Title = @Now_Playing_Title");
                                updateDbCommand.CommandType = CommandType.Text;
                                updateDbCommand.Parameters.AddWithValue("@Now_Playing_Title",
                                sqlReaderCheck["Now_Playing_Title"]);
                                updateDbCommand.Parameters.AddWithValue("@Emailed", "TRUE");
                                updateDbCommand.Connection = connec;
                                updateDbCommand.ExecuteNonQuery();
                                connec.Close();
                            }
                            

                            Console.WriteLine("Email Sent");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Could not end email\n\n" + e);
                        }

                    }
                }
                conn.Close();
                
            }
        }
    }
}
