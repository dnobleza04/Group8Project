using System;
using System.Collections.Generic;
using System.Data;
using System.Web.Services;
using System.Web.Script.Serialization;
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
            public string Status { get; set; }
            public string UpdatedDate { get; set; }
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

        public List<SuggestionRow> GetSuggestions(string statusFilter)
{
    // IMPORTANT: return null when not logged in (so your JS redirects)
    if (Session["loggedIn"] == null || !(bool)Session["loggedIn"])
        return null;

    var rows = new List<SuggestionRow>();

    try
    {
        bool filter = !(string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "All");

        string sql = @"
            SELECT SuggestionID, SuggestionText, CreatedDate, UpdatedDate, UpVotes, DownVotes, Status
            FROM Suggestions
        " + (filter ? " WHERE Status = @Status " : "") + @"
            ORDER BY CreatedDate DESC
            LIMIT 50;
        ";

        using (MySqlConnection con = new MySqlConnection(getConString()))
        using (MySqlCommand cmd = new MySqlCommand(sql, con))
        {
            if (filter)
                cmd.Parameters.AddWithValue("@Status", statusFilter);

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
                        UpdatedDate = rdr["UpdatedDate"] == DBNull.Value ? "" : Convert.ToDateTime(rdr["UpdatedDate"]).ToString("yyyy-MM-dd HH:mm"),
                        UpVotes = Convert.ToInt32(rdr["UpVotes"]),
                        DownVotes = Convert.ToInt32(rdr["DownVotes"]),
                        Status = rdr["Status"] == DBNull.Value ? "Under Review" : rdr["Status"].ToString()
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
       

        // public List<SuggestionRow> GetSuggestions()
        // {
        //     List<SuggestionRow> rows = new List<SuggestionRow>();

        //     if (Session["loggedIn"] == null || !(bool)Session["loggedIn"])
        //     {
        //         return rows; // empty list if not logged in
        //     }

        //     try
        //     {
        //         string sql = @"
        //     SELECT SuggestionID, SuggestionText, CreatedDate, UpVotes, DownVotes
        //     FROM Suggestions
        //     ORDER BY CreatedDate DESC
        //     LIMIT 50;
        // ";

        //         using (MySqlConnection con = new MySqlConnection(getConString()))
        //         using (MySqlCommand cmd = new MySqlCommand(sql, con))
        //         {
        //             con.Open();
        //             using (MySqlDataReader rdr = cmd.ExecuteReader())
        //             {
        //                 while (rdr.Read())
        //                 {
        //                     rows.Add(new SuggestionRow
        //                     {
        //                         SuggestionID = Convert.ToInt32(rdr["SuggestionID"]),
        //                         SuggestionText = rdr["SuggestionText"].ToString(),
        //                         CreatedDate = Convert.ToDateTime(rdr["CreatedDate"]).ToString("yyyy-MM-dd HH:mm"),
        //                         UpVotes = Convert.ToInt32(rdr["UpVotes"]),
        //                         DownVotes = Convert.ToInt32(rdr["DownVotes"])
        //                     });
        //                 }
        //             }
        //         }
        //     }
        //     catch
        //     {
        //         // silent fail
        //     }

        //     return rows;
        // }

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
public BasicResult UpvoteSuggestion(int suggestionID)
{
    BasicResult result = new BasicResult();

    if (Session["loggedIn"] == null || !(bool)Session["loggedIn"])
    {
        result.success = false;
        result.message = "You must be logged in to vote.";
        return result;
    }

    try
    {
        using (MySqlConnection con = new MySqlConnection(getConString()))
        using (MySqlCommand cmd = new MySqlCommand(
            "UPDATE Suggestions SET UpVotes = UpVotes + 1 WHERE SuggestionID = @id;", con))
        {
            cmd.Parameters.AddWithValue("@id", suggestionID);
            con.Open();

            int rows = cmd.ExecuteNonQuery();
            result.success = (rows == 1);
            result.message = rows == 1 ? "Upvoted." : "Suggestion not found.";
        }
    }
    catch (Exception ex)
    {
        result.success = false;
        result.message = "Server error: " + ex.Message;
    }

    return result;
}

//Up/Down vote totals
[WebMethod(EnableSession = true)]
[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
public BasicResult DownvoteSuggestion(int suggestionID)
{
    BasicResult result = new BasicResult();

    if (Session["loggedIn"] == null || !(bool)Session["loggedIn"])
    {
        result.success = false;
        result.message = "You must be logged in to vote.";
        return result;
    }

    try
    {
        using (MySqlConnection con = new MySqlConnection(getConString()))
        using (MySqlCommand cmd = new MySqlCommand(
            "UPDATE Suggestions SET DownVotes = DownVotes + 1 WHERE SuggestionID = @id;", con))
        {
            cmd.Parameters.AddWithValue("@id", suggestionID);
            con.Open();

            int rows = cmd.ExecuteNonQuery();
            result.success = (rows == 1);
            result.message = rows == 1 ? "Downvoted." : "Suggestion not found.";
        }
    }
    catch (Exception ex)
    {
        result.success = false;
        result.message = "Server error: " + ex.Message;
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

         // Participation Summary

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
public object GetDashboardStats()
{
    int totalWorkforce = 50; 

    int employeesWithAnyRequired = 0;
    int totalActivity = 0;

    string sqlEmployeesWithRequired = @"
        SELECT COUNT(DISTINCT r.employee_id)
        FROM responses r
        JOIN question q ON q.question_id = r.question_id
        WHERE q.is_required = 1 AND q.question_type = 'EMPLOYEE';
    ";

    string sqlTotalActivity = @"SELECT COUNT(*) FROM responses;";

    using (MySqlConnection conn = new MySqlConnection(getConString()))
    {
        conn.Open();

        using (var cmd1 = new MySqlCommand(sqlEmployeesWithRequired, conn))
            employeesWithAnyRequired = Convert.ToInt32(cmd1.ExecuteScalar());

        using (var cmd2 = new MySqlCommand(sqlTotalActivity, conn))
            totalActivity = Convert.ToInt32(cmd2.ExecuteScalar());
    }

    int stillNeedToAnswer = Math.Max(0, totalWorkforce - employeesWithAnyRequired);
    int completionPercent = (employeesWithAnyRequired * 100) / totalWorkforce;

    return new
    {
        requiredAnswered = employeesWithAnyRequired,
        stillNeedToAnswer = stillNeedToAnswer,
        totalActivity = totalActivity,
        completionPercent = completionPercent
    };
}

[WebMethod]
[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
public object GetEmployeeQuestions()
{
    var list = new List<object>();

    string sql = @"
        SELECT question_id, question_text, is_required
        FROM question
        WHERE question_type = 'EMPLOYEE'
        ORDER BY question_id;
    ";

    using (MySqlConnection conn = new MySqlConnection(getConString()))
    {
        conn.Open();
        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        using (MySqlDataReader rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
            {
                list.Add(new
                {
                    id = rdr.GetInt32("question_id"),
                    text = rdr.GetString("question_text"),
                    required = rdr.GetInt32("is_required") == 1,
                    type = "text" 
                });
            }
        }
    }

    return list;
}


public class ResponseDTO
{
    public int question_id { get; set; }
    public string response_text { get; set; }
    public string topic { get; set; }
}

[WebMethod(EnableSession = true)]
[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
public object SubmitResponses(List<ResponseDTO> responses)
{
    if (Session["employee_id"] == null)
        return new { ok = false, message = "Not logged in (missing session employee_id)." };

    int employeeId = Convert.ToInt32(Session["employee_id"]);

    string insertSql = @"
        INSERT INTO responses (question_id, response_text, created_at, employee_id, topic)
        VALUES (@qid, @text, NOW(), @emp, @topic);
    ";

    int inserted = 0;

    using (MySqlConnection conn = new MySqlConnection(getConString()))
    {
        conn.Open();

        foreach (var r in responses)
        {
            // skip blanks 
            if (r == null) continue;
            string text = (r.response_text ?? "").Trim();
            if (text.Length == 0) continue;

            string topic = (r.topic ?? "General").Trim();
            if (topic.Length == 0) topic = "General";

            using (MySqlCommand cmd = new MySqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@qid", r.question_id);
                cmd.Parameters.AddWithValue("@text", text);
                cmd.Parameters.AddWithValue("@emp", employeeId);
                cmd.Parameters.AddWithValue("@topic", topic);

                inserted += cmd.ExecuteNonQuery();
            }
        }
    }

    return new { ok = true, inserted = inserted };

}

// Idea Tracking
public class SuggestionDTO
{
    public int SuggestionID { get; set; }
    public string SuggestionText { get; set; }
    public string Status { get; set; }
    public int UpVotes { get; set; }
    public int DownVotes { get; set; }
    public string CreatedDate { get; set; }
    public string UpdatedDate { get; set; }
}

[WebMethod]
[System.Web.Script.Services.ScriptMethod(ResponseFormat = System.Web.Script.Services.ResponseFormat.Json)]
public List<SuggestionDTO> GetSuggestions(string statusFilter)
{
    var results = new List<SuggestionDTO>();

    using (var con = new MySql.Data.MySqlClient.MySqlConnection(getConString()))
    {
        con.Open();

        bool filter = !(string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "All");

        string sql = @"
            SELECT 
                SuggestionID,
                SuggestionText,
                Status,
                UpVotes,
                DownVotes,
                DATE_FORMAT(CreatedDate, '%Y-%m-%d %H:%i:%s') AS CreatedDate,
                DATE_FORMAT(UpdatedDate, '%Y-%m-%d %H:%i:%s') AS UpdatedDate
            FROM Suggestions
        " + (filter ? " WHERE Status = @Status " : "") + @"
            ORDER BY CreatedDate DESC;
        ";

        using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, con))
        {
            if (filter)
                cmd.Parameters.AddWithValue("@Status", statusFilter);

            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    results.Add(new SuggestionDTO
                    {
                        SuggestionID = Convert.ToInt32(rdr["SuggestionID"]),
                        SuggestionText = rdr["SuggestionText"].ToString(),
                        Status = rdr["Status"].ToString(),
                        UpVotes = Convert.ToInt32(rdr["UpVotes"]),
                        DownVotes = Convert.ToInt32(rdr["DownVotes"]),
                        CreatedDate = rdr["CreatedDate"].ToString(),
                        UpdatedDate = rdr["UpdatedDate"].ToString()
                    });
                }
            }
        }
    }

    return results;
}

//filter 
[WebMethod]
[System.Web.Script.Services.ScriptMethod(ResponseFormat = System.Web.Script.Services.ResponseFormat.Json)]
[WebMethod(EnableSession = true)]
[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
public BasicResult UpdateSuggestionStatus(int suggestionID, string status)
{
    BasicResult result = new BasicResult();

    if (Session["loggedIn"] == null || !(bool)Session["loggedIn"])
    {
        result.success = false;
        result.message = "You must be logged in.";
        return result;
    }

    var allowed = new HashSet<string> { "Under Review", "Planned", "Implemented" };
    if (!allowed.Contains(status))
    {
        result.success = false;
        result.message = "Invalid status.";
        return result;
    }

    try
    {
        using (MySqlConnection con = new MySqlConnection(getConString()))
        using (MySqlCommand cmd = new MySqlCommand(@"
            UPDATE Suggestions
            SET Status = @Status
            WHERE SuggestionID = @SuggestionID;", con))
        {
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@SuggestionID", suggestionID);

            con.Open();
            int rows = cmd.ExecuteNonQuery();

            result.success = (rows == 1);
            result.message = rows == 1 ? "Status updated." : "Suggestion not found.";
        }
    }
    catch (Exception ex)
    {
        result.success = false;
        result.message = "Server error: " + ex.Message;
    }

    return result;
}

//upvotes
[WebMethod]
[System.Web.Script.Services.ScriptMethod(ResponseFormat = System.Web.Script.Services.ResponseFormat.Json)]
public string UpvoteSuggestion(int suggestionID)
{
    using (var con = new MySql.Data.MySqlClient.MySqlConnection(getConString()))
    {
        con.Open();

        string sql = @"UPDATE Suggestions SET UpVotes = UpVotes + 1 WHERE SuggestionID = @SuggestionID;";

        using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, con))
        {
            cmd.Parameters.AddWithValue("@SuggestionID", suggestionID);
            int rows = cmd.ExecuteNonQuery();
            return rows == 1 ? "OK" : "ERROR: Not found";
        }
    }
}

//downvote 
[WebMethod]
[System.Web.Script.Services.ScriptMethod(ResponseFormat = System.Web.Script.Services.ResponseFormat.Json)]
public string DownvoteSuggestion(int suggestionID)
{
    using (var con = new MySql.Data.MySqlClient.MySqlConnection(getConString()))
    {
        con.Open();

        string sql = @"UPDATE Suggestions SET DownVotes = DownVotes + 1 WHERE SuggestionID = @SuggestionID;";

        using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, con))
        {
            cmd.Parameters.AddWithValue("@SuggestionID", suggestionID);
            int rows = cmd.ExecuteNonQuery();
            return rows == 1 ? "OK" : "ERROR: Not found";
        }
    }
}

    }
}