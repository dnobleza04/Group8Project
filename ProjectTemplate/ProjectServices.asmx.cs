using System;
using System.Collections.Generic;
using System.Web.Services;
using System.Web.Script.Services;
using MySql.Data.MySqlClient;

namespace ProjectTemplate
{
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [ScriptService] // REQUIRED so JS (AJAX) can call the methods
    public class ProjectServices : WebService
    {
        // DB creds
        private string dbID = "cis440Spring2026team8";
        private string dbPass = "cis440Spring2026team8";
        private string dbName = "cis440Spring2026team8";

        private string getConString()
        {
            return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName + "; UID=" + dbID + "; PASSWORD=" + dbPass;
        }

        // -------------------------
        // Models (JSON-friendly)
        // -------------------------
        public class LoginResult
        {
            public bool success { get; set; }
            public string message { get; set; }
        }

        public class BasicResult
        {
            public bool success { get; set; }
            public string message { get; set; }
        }

        public class SuggestionRow
        {
            public int SuggestionID { get; set; }
            public string SuggestionText { get; set; }
            public string CreatedDate { get; set; }   // send as string for easy display
            public int UpVotes { get; set; }
            public int DownVotes { get; set; }
        }

        // -------------------------
        // Utility: ensure anon id
        // -------------------------
        private string EnsureAnonId()
        {
            if (Session["anonId"] == null)
            {
                Session["anonId"] = Guid.NewGuid().ToString();
            }
            return Session["anonId"].ToString();
        }

        // -------------------------
        // Test
        // -------------------------
        [WebMethod(EnableSession = true)]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string TestConnection()
        {
            return "Success!";
        }

        // -------------------------
        // Employee Login
        // -------------------------
        [WebMethod(EnableSession = true)]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public LoginResult EmployeeLogin(string email, string password)
        {
            LoginResult result = new LoginResult();

            try
            {
                string sql = @"
                    SELECT employee_ID
                    FROM employee
                    WHERE email = @email
                      AND password_hash = SHA2(@password, 256)
                    LIMIT 1;
                ";

                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@password", password);

                    con.Open();
                    object employeeIdObj = cmd.ExecuteScalar();

                    if (employeeIdObj == null)
                    {
                        result.success = false;
                        result.message = "Incorrect email or password. Try again.";
                        return result;
                    }

                    Session["loggedIn"] = true;
                    Session["employeeId"] = Convert.ToInt32(employeeIdObj);

                    // anonymous identifier (NOT employee id)
                    EnsureAnonId();

                    result.success = true;
                    result.message = "Login successful.";
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = "Server error: " + ex.Message;
                return result;
            }
        }

        // -------------------------
        // Suggestions: Add
        // -------------------------
        [WebMethod(EnableSession = true)]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public BasicResult AddSuggestion(string suggestionText)
        {
            BasicResult result = new BasicResult();

            try
            {
                // Basic validation
                if (suggestionText == null) suggestionText = "";
                suggestionText = suggestionText.Trim();

                if (suggestionText.Length == 0)
                {
                    result.success = false;
                    result.message = "Suggestion cannot be empty.";
                    return result;
                }

                // Optional: require login (recommended)
                if (Session["loggedIn"] == null || (bool)Session["loggedIn"] == false)
                {
                    result.success = false;
                    result.message = "You must be logged in to submit a suggestion.";
                    return result;
                }

                // Make sure we have anon id (for later tracking if needed)
                EnsureAnonId();

                string sql = @"
                    INSERT INTO Suggestions (SuggestionText, CreatedDate, UpVotes, DownVotes)
                    VALUES (@text, NOW(), 0, 0);
                ";

                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@text", suggestionText);

                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                result.success = true;
                result.message = "Suggestion submitted.";
                return result;
            }
            catch (Exception ex)
            {
                result.success = false;
                result.message = "Server error: " + ex.Message;
                return result;
            }
        }

        // -------------------------
        // Suggestions: Get list
        // -------------------------
        [WebMethod(EnableSession = true)]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public List<SuggestionRow> GetSuggestions()
        {
            List<SuggestionRow> rows = new List<SuggestionRow>();

            if (Session["loggedIn"] == null || !(bool)Session["loggedIn"])
            {
                return rows; // empty list if not logged in
            }

            try
            {
                string sql = @"
            SELECT SuggestionID, SuggestionText, CreatedDate, UpVotes, DownVotes
            FROM Suggestions
            ORDER BY CreatedDate DESC
            LIMIT 50;
        ";

                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(sql, con))
                {
                    con.Open();
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            rows.Add(new SuggestionRow
                            {
                                SuggestionID = Convert.ToInt32(rdr["SuggestionID"]),
                                SuggestionText = rdr["SuggestionText"].ToString(),
                                CreatedDate = Convert.ToDateTime(rdr["CreatedDate"]).ToString("yyyy-MM-dd HH:mm"),
                                UpVotes = Convert.ToInt32(rdr["UpVotes"]),
                                DownVotes = Convert.ToInt32(rdr["DownVotes"])
                            });
                        }
                    }
                }
            }
            catch
            {
                // silent fail
            }

            return rows;
        }

        // -------------------------
        // Suggestions: Vote
        // -------------------------
        [WebMethod(EnableSession = true)]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public BasicResult VoteSuggestion(int suggestionId, bool isUpvote)
        {
            BasicResult result = new BasicResult();

            if (Session["loggedIn"] == null || !(bool)Session["loggedIn"])
            {
                result.success = false;
                result.message = "You must be logged in to vote.";
                return result;
            }

            string anonId = Session["anonId"].ToString();

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                {
                    con.Open();

                    // Try inserting vote
                    string insertVote = @"
                INSERT INTO SuggestionVotes (SuggestionID, AnonID, IsUpvote)
                VALUES (@id, @anonId, @isUpvote);
            ";

                    using (MySqlCommand cmd = new MySqlCommand(insertVote, con))
                    {
                        cmd.Parameters.AddWithValue("@id", suggestionId);
                        cmd.Parameters.AddWithValue("@anonId", anonId);
                        cmd.Parameters.AddWithValue("@isUpvote", isUpvote);

                        cmd.ExecuteNonQuery();
                    }

                    // Update counts
                    string updateSql = isUpvote
                        ? "UPDATE Suggestions SET UpVotes = UpVotes + 1 WHERE SuggestionID = @id"
                        : "UPDATE Suggestions SET DownVotes = DownVotes + 1 WHERE SuggestionID = @id";

                    using (MySqlCommand updateCmd = new MySqlCommand(updateSql, con))
                    {
                        updateCmd.Parameters.AddWithValue("@id", suggestionId);
                        updateCmd.ExecuteNonQuery();
                    }

                    result.success = true;
                    result.message = "Vote recorded.";
                }
            }
            catch (MySqlException)
            {
                result.success = false;
                result.message = "You have already voted on this suggestion.";
            }

            return result;
        }
        [WebMethod(EnableSession = true)]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public BasicResult Logout()
        {
            BasicResult result = new BasicResult();

            Session.Clear();
            result.success = true;
            result.message = "Logged out.";

            return result;
        }
    }
}