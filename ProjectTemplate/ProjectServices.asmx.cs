using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Services;
using System.Web.Script.Services;
using MySql.Data.MySqlClient;

namespace ProjectTemplate
{
	[WebService(Namespace = "http://tempuri.org/")]
	[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
	[System.ComponentModel.ToolboxItem(false)]
	[ScriptService]
	public class ProjectServices : WebService
	{
		private string dbID = "cis440Spring2026team8";
		private string dbPass = "cis440Spring2026team8";
		private string dbName = "cis440Spring2026team8";

		private string getConString()
		{
			return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName + "; UID=" + dbID + "; PASSWORD=" + dbPass;
		}

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

		public class AccessStatusResult
		{
			public bool loggedIn { get; set; }
			public bool verified { get; set; }
			public string message { get; set; }
		}

		public class SuggestionRow
		{
			public int SuggestionID { get; set; }
			public string SuggestionText { get; set; }
			public string CreatedDate { get; set; }
			public int UpVotes { get; set; }
			public int DownVotes { get; set; }
		}

		public class IdeaRow
		{
			public int IdeaID { get; set; }
			public string IdeaText { get; set; }
			public string CreatedDate { get; set; }
			public int UpVotes { get; set; }
			public int DownVotes { get; set; }
		}

		public class VoteQuestionRow
		{
			public int QuestionID { get; set; }
			public string QuestionText { get; set; }
			public string CreatedDate { get; set; }
			public int RightVotes { get; set; }
			public int LeftVotes { get; set; }
			public bool Retired { get; set; }
		}

		public class VoteQuestionResult
		{
			public bool success { get; set; }
			public string message { get; set; }
			public bool hasNext { get; set; }
			public VoteQuestionRow nextQuestion { get; set; }
		}

		public class VoteRequirementResult
		{
			public bool completed { get; set; }
		}

		public class IdeaVoteResult
		{
			public bool success { get; set; }
			public string message { get; set; }
			public bool hasNext { get; set; }
			public IdeaRow nextIdea { get; set; }
		}

		private string EnsureAnonId()
		{
			if (Session["anonId"] == null)
			{
				Session["anonId"] = Guid.NewGuid().ToString();
			}
			return Session["anonId"].ToString();
		}

		private bool IsLoggedInSession()
		{
			return Session["loggedIn"] != null && (bool)Session["loggedIn"];
		}

		private bool IsVerifiedSession()
		{
			return IsLoggedInSession() && Session["verified"] != null && (bool)Session["verified"];
		}

		private HashSet<int> GetSkippedQuestionIdsSession()
		{
			if (Session["skippedQuestionIds"] == null)
			{
				Session["skippedQuestionIds"] = new HashSet<int>();
			}

			return (HashSet<int>)Session["skippedQuestionIds"];
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public string TestConnection()
		{
			return "Success!";
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public LoginResult EmployeeLogin(string email, string password)
		{
			LoginResult result = new LoginResult();
			string loginStep = "start";

			try
			{
				loginStep = "open_connection";
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();

					Func<object, bool> parseVerified = (verifiedObj) =>
					{
						if (verifiedObj == null || verifiedObj == DBNull.Value)
						{
							return true;
						}

						if (verifiedObj is bool)
						{
							return (bool)verifiedObj;
						}

						string verifiedText = Convert.ToString(verifiedObj);
						return verifiedText == "1"
							|| verifiedText.Equals("true", StringComparison.OrdinalIgnoreCase)
							|| verifiedText.Equals("y", StringComparison.OrdinalIgnoreCase)
							|| verifiedText.Equals("yes", StringComparison.OrdinalIgnoreCase);
					};

					Func<string, bool, bool> tryLoginQuery = (verifyColumn, hashPassword) =>
					{
						string passwordExpr = hashPassword
							? "password_hash = SHA2(@password, 256)"
							: "password = @password";

						string sql = (verifyColumn != null)
							? "SELECT employee_ID, " + verifyColumn + " FROM employee WHERE email = @email AND " + passwordExpr + " LIMIT 1;"
							: "SELECT employee_ID, 1 FROM employee WHERE email = @email AND " + passwordExpr + " LIMIT 1;";

						try
						{
							loginStep = "execute_login_query";
							using (MySqlCommand cmd = new MySqlCommand(sql, con))
							{
								cmd.Parameters.AddWithValue("@email", email);
								cmd.Parameters.AddWithValue("@password", password);

								using (MySqlDataReader rdr = cmd.ExecuteReader())
								{
									if (!rdr.Read())
									{
										return false;
									}

									int employeeId = Convert.ToInt32(rdr.GetValue(0));
									bool isVerified = parseVerified(rdr.GetValue(1));

									if (!isVerified)
									{
										Session.Clear();
										result.success = false;
										result.message = "Access Denied";
										return true;
									}

									Session["loggedIn"] = true;
									Session["verified"] = true;
									Session["employeeId"] = employeeId;
									loginStep = "ensure_anon_id";
									EnsureAnonId();

									result.success = true;
									result.message = "Login successful.";
									return true;
								}
							}
						}
						catch (MySqlException ex)
						{
							if (ex.Number == 1054 || ex.Message.IndexOf("Unknown column", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								return false;
							}

							throw;
						}
					};

					if (tryLoginQuery("is_verified", true)) return result;
					if (tryLoginQuery("verified", true)) return result;
					if (tryLoginQuery("isVerified", true)) return result;
					if (tryLoginQuery("IsVerified", true)) return result;
					if (tryLoginQuery(null, true)) return result;

					if (tryLoginQuery("is_verified", false)) return result;
					if (tryLoginQuery("verified", false)) return result;
					if (tryLoginQuery("isVerified", false)) return result;
					if (tryLoginQuery("IsVerified", false)) return result;
					if (tryLoginQuery(null, false)) return result;

					result.success = false;
					result.message = "Incorrect email or password. Try again.";
					return result;
				}
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Server error at " + loginStep + ": " + ex.GetType().Name + " - " + ex.Message;
				return result;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public AccessStatusResult GetAccessStatus()
		{
			AccessStatusResult result = new AccessStatusResult();

			bool loggedIn = IsLoggedInSession();
			bool verified = IsVerifiedSession();

			result.loggedIn = loggedIn;
			result.verified = verified;

			if (!loggedIn)
			{
				result.message = "Not logged in.";
			}
			else if (!verified)
			{
				result.message = "Access Denied";
			}
			else
			{
				result.message = "OK";
			}

			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult AddSuggestion(string suggestionText)
		{
			BasicResult result = new BasicResult();

			try
			{
				if (suggestionText == null) suggestionText = "";
				suggestionText = suggestionText.Trim();

				if (suggestionText.Length == 0)
				{
					result.success = false;
					result.message = "Suggestion cannot be empty.";
					return result;
				}

				if (!IsLoggedInSession())
				{
					result.success = false;
					result.message = "You must be logged in to submit a suggestion.";
					return result;
				}

				if (!IsVerifiedSession())
				{
					result.success = false;
					result.message = "Access Denied";
					return result;
				}

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

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult SubmitAnonymousFeedback(string feedbackText)
		{
			return AddSuggestion(feedbackText);
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public List<SuggestionRow> GetSuggestions()
		{
			List<SuggestionRow> rows = new List<SuggestionRow>();

			if (!IsVerifiedSession())
			{
				return rows;
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
			}

			return rows;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult AddIdea(string ideaText)
		{
			BasicResult result = new BasicResult();

			try
			{
				if (ideaText == null) ideaText = "";
				ideaText = ideaText.Trim();

				if (ideaText.Length == 0)
				{
					result.success = false;
					result.message = "Please enter your idea.";
					return result;
				}

				if (!IsLoggedInSession())
				{
					result.success = false;
					result.message = "You must be logged in to submit an idea.";
					return result;
				}

				if (!IsVerifiedSession())
				{
					result.success = false;
					result.message = "Access Denied";
					return result;
				}

				string sql = @"
					INSERT INTO Ideas (IdeasText, CreatedDate, UpVotes, DownVotes)
					VALUES (@text, NOW(), 0, 0);
				";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				using (MySqlCommand cmd = new MySqlCommand(sql, con))
				{
					cmd.Parameters.AddWithValue("@text", ideaText);
					con.Open();
					cmd.ExecuteNonQuery();
				}

				result.success = true;
				result.message = "Idea submitted. Peers can vote on it from the dashboard.";
				return result;
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Server error: " + ex.Message;
				return result;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public List<IdeaRow> GetIdeas()
		{
			List<IdeaRow> rows = new List<IdeaRow>();

			// Allow any logged-in user (dashboard Suggestions section shows Ideas table)
			if (!IsLoggedInSession())
			{
				return rows;
			}

			try
			{
				string sql = @"
					SELECT IdeasID AS IdeaID, IdeasText AS IdeaText, CreatedDate, UpVotes, DownVotes
					FROM Ideas
					WHERE CreatedDate >= DATE_SUB(NOW(), INTERVAL 7 DAY)
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
							rows.Add(new IdeaRow
							{
								IdeaID = Convert.ToInt32(rdr["IdeaID"]),
								IdeaText = rdr["IdeaText"].ToString(),
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
			}

			return rows;
		}

		private IdeaRow GetNextIdeaForAnon(string anonId, int? skipIdeaId = null)
		{
			string sql = @"
				SELECT IdeasID AS IdeaID, IdeasText AS IdeaText, CreatedDate, UpVotes, DownVotes
				FROM Ideas
				WHERE CreatedDate >= DATE_SUB(NOW(), INTERVAL 7 DAY)
				  AND NOT EXISTS (
					SELECT 1 FROM IdeasVote v
					WHERE v.IdeaID = Ideas.IdeasID AND v.AnonID = @anonId
				)
			";
			if (skipIdeaId.HasValue)
			{
				sql += " AND Ideas.IdeasID != @skipId";
			}
			sql += " ORDER BY Ideas.CreatedDate DESC LIMIT 1;";

			using (MySqlConnection con = new MySqlConnection(getConString()))
			using (MySqlCommand cmd = new MySqlCommand(sql, con))
			{
				cmd.Parameters.AddWithValue("@anonId", anonId);
				if (skipIdeaId.HasValue)
				{
					cmd.Parameters.AddWithValue("@skipId", skipIdeaId.Value);
				}
				con.Open();
				using (MySqlDataReader rdr = cmd.ExecuteReader())
				{
					if (!rdr.Read())
					{
						return null;
					}
					return new IdeaRow
					{
						IdeaID = Convert.ToInt32(rdr["IdeaID"]),
						IdeaText = rdr["IdeaText"].ToString(),
						CreatedDate = Convert.ToDateTime(rdr["CreatedDate"]).ToString("yyyy-MM-dd HH:mm"),
						UpVotes = Convert.ToInt32(rdr["UpVotes"]),
						DownVotes = Convert.ToInt32(rdr["DownVotes"])
					};
				}
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public IdeaRow GetNextIdeaForVote()
		{
			if (!IsVerifiedSession())
			{
				return null;
			}
			try
			{
				string anonId = EnsureAnonId();
				return GetNextIdeaForAnon(anonId, null);
			}
			catch
			{
				return null;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public IdeaVoteResult VoteIdea(int ideaId, bool isUpvote)
		{
			IdeaVoteResult result = new IdeaVoteResult();

			if (!IsLoggedInSession())
			{
				result.success = false;
				result.message = "You must be logged in to vote.";
				result.hasNext = false;
				return result;
			}

			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				result.hasNext = false;
				return result;
			}

			string anonId = EnsureAnonId();

			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					using (MySqlTransaction tx = con.BeginTransaction())
					{
						string existsSql = "SELECT COUNT(*) FROM Ideas WHERE IdeasID = @id";
						using (MySqlCommand existsCmd = new MySqlCommand(existsSql, con, tx))
						{
							existsCmd.Parameters.AddWithValue("@id", ideaId);
							if (Convert.ToInt32(existsCmd.ExecuteScalar()) == 0)
							{
								tx.Rollback();
								result.success = false;
								result.message = "Idea not found.";
								result.hasNext = false;
								return result;
							}
						}

						string alreadySql = "SELECT COUNT(*) FROM IdeasVote WHERE IdeaID = @id AND AnonID = @anonId";
						using (MySqlCommand alreadyCmd = new MySqlCommand(alreadySql, con, tx))
						{
							alreadyCmd.Parameters.AddWithValue("@id", ideaId);
							alreadyCmd.Parameters.AddWithValue("@anonId", anonId);
							if (Convert.ToInt32(alreadyCmd.ExecuteScalar()) > 0)
							{
								tx.Rollback();
								result.success = false;
								result.message = "You have already voted on this idea.";
								result.hasNext = false;
								return result;
							}
						}

						string insertSql = "INSERT INTO IdeasVote (IdeaID, AnonID, IsUpvote) VALUES (@id, @anonId, @isUpvote)";
						using (MySqlCommand cmd = new MySqlCommand(insertSql, con, tx))
						{
							cmd.Parameters.AddWithValue("@id", ideaId);
							cmd.Parameters.AddWithValue("@anonId", anonId);
							cmd.Parameters.AddWithValue("@isUpvote", isUpvote);
							cmd.ExecuteNonQuery();
						}

						string updateSql = isUpvote
							? "UPDATE Ideas SET UpVotes = UpVotes + 1 WHERE IdeasID = @id"
							: "UPDATE Ideas SET DownVotes = DownVotes + 1 WHERE IdeasID = @id";
						using (MySqlCommand updateCmd = new MySqlCommand(updateSql, con, tx))
						{
							updateCmd.Parameters.AddWithValue("@id", ideaId);
							updateCmd.ExecuteNonQuery();
						}

						tx.Commit();
					}
				}

				result.success = true;
				result.message = "Vote recorded.";
				result.nextIdea = GetNextIdeaForAnon(anonId, null);
				result.hasNext = result.nextIdea != null;
			}
			catch (MySqlException)
			{
				result.success = false;
				result.message = "Vote could not be recorded.";
				result.hasNext = false;
			}

			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public IdeaVoteResult SkipIdea(int ideaId)
		{
			IdeaVoteResult result = new IdeaVoteResult();

			if (!IsLoggedInSession())
			{
				result.success = false;
				result.message = "You must be logged in.";
				result.hasNext = false;
				return result;
			}

			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				result.hasNext = false;
				return result;
			}

			string anonId = EnsureAnonId();
			result.success = true;
			result.message = "Skipped.";
			result.nextIdea = GetNextIdeaForAnon(anonId, ideaId);
			result.hasNext = result.nextIdea != null;
			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult DeleteIdea(int ideaId)
		{
			BasicResult result = new BasicResult();
			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				return result;
			}
			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					using (MySqlTransaction tx = con.BeginTransaction())
					{
						using (MySqlCommand cmd = new MySqlCommand("DELETE FROM IdeasVote WHERE IdeaID = @id", con, tx))
						{
							cmd.Parameters.AddWithValue("@id", ideaId);
							cmd.ExecuteNonQuery();
						}
						using (MySqlCommand cmd = new MySqlCommand("DELETE FROM Ideas WHERE IdeasID = @id", con, tx))
						{
							cmd.Parameters.AddWithValue("@id", ideaId);
							cmd.ExecuteNonQuery();
						}
						tx.Commit();
					}
				}
				result.success = true;
				result.message = "Idea deleted.";
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Could not delete: " + ex.Message;
			}
			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult AddVoteQuestion(string questionText)
		{
			BasicResult result = new BasicResult();

			try
			{
				if (questionText == null) questionText = "";
				questionText = questionText.Trim();

				if (questionText.Length == 0)
				{
					result.success = false;
					result.message = "Question text cannot be empty.";
					return result;
				}

				if (!IsVerifiedSession())
				{
					result.success = false;
					result.message = "Access Denied";
					return result;
				}

				string sql = @"
					INSERT INTO AdminQuestions (QuestionText, CreatedDate, YesVotes, NoVotes)
					VALUES (@text, NOW(), 0, 0);
				";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				using (MySqlCommand cmd = new MySqlCommand(sql, con))
				{
					cmd.Parameters.AddWithValue("@text", questionText);
					con.Open();
					cmd.ExecuteNonQuery();
				}

				result.success = true;
				result.message = "Question added. Employees will see it in Vote Here.";
				return result;
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Server error: " + ex.Message;
				return result;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public List<VoteQuestionRow> GetVoteQuestions()
		{
			List<VoteQuestionRow> rows = new List<VoteQuestionRow>();

			if (!IsVerifiedSession())
			{
				return rows;
			}

			try
			{
				string sql = @"
					SELECT q.AdminQuestionID AS QuestionID, q.QuestionText, q.CreatedDate, q.YesVotes AS RightVotes, q.NoVotes AS LeftVotes,
					       IF(EXISTS(SELECT 1 FROM AdminQuestionsRetired r WHERE r.AdminQuestionID = q.AdminQuestionID), 1, 0) AS Retired
					FROM AdminQuestions q
					ORDER BY CreatedDate DESC
					LIMIT 100;
				";

				using (MySqlConnection con = new MySqlConnection(getConString()))
				using (MySqlCommand cmd = new MySqlCommand(sql, con))
				{
					con.Open();
					using (MySqlDataReader rdr = cmd.ExecuteReader())
					{
						while (rdr.Read())
						{
							object retiredObj = rdr["Retired"];
							bool retired = (retiredObj != null && retiredObj != DBNull.Value && Convert.ToInt32(retiredObj) != 0);
							rows.Add(new VoteQuestionRow
							{
								QuestionID = Convert.ToInt32(rdr["QuestionID"]),
								QuestionText = rdr["QuestionText"].ToString(),
								CreatedDate = Convert.ToDateTime(rdr["CreatedDate"]).ToString("yyyy-MM-dd HH:mm"),
								RightVotes = Convert.ToInt32(rdr["RightVotes"]),
								LeftVotes = Convert.ToInt32(rdr["LeftVotes"]),
								Retired = retired
							});
						}
					}
				}
			}
			catch
			{
			}

			return rows;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult RetireVoteQuestion(int questionId)
		{
			BasicResult result = new BasicResult();
			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				return result;
			}
			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					string sql = "INSERT INTO AdminQuestionsRetired (AdminQuestionID, RetiredAt) VALUES (@id, NOW()) ON DUPLICATE KEY UPDATE RetiredAt = NOW()";
					using (MySqlCommand cmd = new MySqlCommand(sql, con))
					{
						cmd.Parameters.AddWithValue("@id", questionId);
						cmd.ExecuteNonQuery();
					}
				}
				result.success = true;
				result.message = "Question retired.";
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Could not retire: " + ex.Message;
			}
			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult ActivateVoteQuestion(int questionId)
		{
			BasicResult result = new BasicResult();
			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				return result;
			}
			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				using (MySqlCommand cmd = new MySqlCommand("DELETE FROM AdminQuestionsRetired WHERE AdminQuestionID = @id", con))
				{
					cmd.Parameters.AddWithValue("@id", questionId);
					con.Open();
					cmd.ExecuteNonQuery();
				}
				result.success = true;
				result.message = "Question activated.";
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Could not activate: " + ex.Message;
			}
			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult DeleteVoteQuestion(int questionId)
		{
			BasicResult result = new BasicResult();
			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				return result;
			}
			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					using (MySqlTransaction tx = con.BeginTransaction())
					{
						using (MySqlCommand cmd = new MySqlCommand("DELETE FROM AdminQuestionsVotes WHERE AdminQuestionID = @id", con, tx))
						{
							cmd.Parameters.AddWithValue("@id", questionId);
							cmd.ExecuteNonQuery();
						}
						using (MySqlCommand cmd = new MySqlCommand("DELETE FROM AdminQuestions WHERE AdminQuestionID = @id", con, tx))
						{
							cmd.Parameters.AddWithValue("@id", questionId);
							cmd.ExecuteNonQuery();
						}
						tx.Commit();
					}
				}
				result.success = true;
				result.message = "Question deleted.";
			}
			catch (Exception ex)
			{
				result.success = false;
				result.message = "Could not delete: " + ex.Message;
			}
			return result;
		}

		private VoteQuestionRow GetNextVoteQuestionForAnon(string anonId, HashSet<int> skippedIds = null)
		{
			string sql = @"
				SELECT
					q.AdminQuestionID AS QuestionID,
					q.QuestionText,
					q.CreatedDate,
					q.YesVotes AS RightVotes,
					q.NoVotes AS LeftVotes
				FROM AdminQuestions q
				WHERE q.CreatedDate >= DATE_SUB(NOW(), INTERVAL 7 DAY)
				  AND NOT EXISTS (SELECT 1 FROM AdminQuestionsRetired r WHERE r.AdminQuestionID = q.AdminQuestionID)
				  AND NOT EXISTS (
					SELECT 1
					FROM AdminQuestionsVotes vq
					WHERE vq.AdminQuestionID = q.AdminQuestionID
					  AND vq.AnonID = @anonId
				)
			";

			if (skippedIds != null && skippedIds.Count > 0)
			{
				string[] skipParams = skippedIds.Select((id, i) => "@skip" + i).ToArray();
				sql += " AND q.AdminQuestionID NOT IN (" + string.Join(",", skipParams) + ")";
			}

			sql += " ORDER BY q.CreatedDate DESC LIMIT 1;";

			using (MySqlConnection con = new MySqlConnection(getConString()))
			using (MySqlCommand cmd = new MySqlCommand(sql, con))
			{
				cmd.Parameters.AddWithValue("@anonId", anonId);

				if (skippedIds != null && skippedIds.Count > 0)
				{
					int i = 0;
					foreach (int id in skippedIds)
					{
						cmd.Parameters.AddWithValue("@skip" + i, id);
						i++;
					}
				}

				con.Open();

				using (MySqlDataReader rdr = cmd.ExecuteReader())
				{
					if (!rdr.Read())
					{
						return null;
					}

					return new VoteQuestionRow
					{
						QuestionID = Convert.ToInt32(rdr["QuestionID"]),
						QuestionText = rdr["QuestionText"].ToString(),
						CreatedDate = Convert.ToDateTime(rdr["CreatedDate"]).ToString("yyyy-MM-dd HH:mm"),
						RightVotes = Convert.ToInt32(rdr["RightVotes"]),
						LeftVotes = Convert.ToInt32(rdr["LeftVotes"])
					};
				}
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public VoteQuestionRow GetNextVoteQuestion()
		{
			if (!IsVerifiedSession())
			{
				return null;
			}

			try
			{
				string anonId = EnsureAnonId();
				HashSet<int> skippedIds = GetSkippedQuestionIdsSession();
				return GetNextVoteQuestionForAnon(anonId, skippedIds);
			}
			catch
			{
				return null;
			}
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public VoteRequirementResult HasCompletedVoteRequirement()
		{
			var result = new VoteRequirementResult { completed = false };
			if (!IsVerifiedSession())
				return result;
			try
			{
				string anonId = EnsureAnonId();
				HashSet<int> skippedIds = GetSkippedQuestionIdsSession();
				VoteQuestionRow next = GetNextVoteQuestionForAnon(anonId, skippedIds);
				result.completed = (next == null);
			}
			catch { }
			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public VoteQuestionResult SkipQuestion(int questionId)
		{
			VoteQuestionResult result = new VoteQuestionResult();

			if (!IsLoggedInSession())
			{
				result.success = false;
				result.message = "You must be logged in to skip.";
				result.hasNext = false;
				return result;
			}

			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				result.hasNext = false;
				return result;
			}

			if (questionId <= 0)
			{
				result.success = false;
				result.message = "Invalid question.";
				result.hasNext = false;
				return result;
			}

			string anonId = EnsureAnonId();
			HashSet<int> skippedIds = GetSkippedQuestionIdsSession();
			skippedIds.Add(questionId);

			result.success = true;
			result.message = "Question skipped.";

			try
			{
				result.nextQuestion = GetNextVoteQuestionForAnon(anonId, skippedIds);
				result.hasNext = result.nextQuestion != null;
			}
			catch
			{
				result.nextQuestion = null;
				result.hasNext = false;
			}

			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public VoteQuestionResult VoteQuestion(int questionId, bool voteRight)
		{
			VoteQuestionResult result = new VoteQuestionResult();

			if (!IsLoggedInSession())
			{
				result.success = false;
				result.message = "You must be logged in to vote.";
				result.hasNext = false;
				return result;
			}

			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				result.hasNext = false;
				return result;
			}

			string anonId = EnsureAnonId();
			HashSet<int> skippedIds = GetSkippedQuestionIdsSession();

			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();
					using (MySqlTransaction tx = con.BeginTransaction())
					{
						string existsSql = "SELECT COUNT(*) FROM AdminQuestions WHERE AdminQuestionID = @id";

						using (MySqlCommand existsCmd = new MySqlCommand(existsSql, con, tx))
						{
							existsCmd.Parameters.AddWithValue("@id", questionId);
							int exists = Convert.ToInt32(existsCmd.ExecuteScalar());
							if (exists == 0)
							{
								tx.Rollback();
								result.success = false;
								result.message = "Question not found.";
								result.hasNext = false;
								return result;
							}
						}

						string alreadySql = @"
							SELECT COUNT(*)
							FROM AdminQuestionsVotes
							WHERE AdminQuestionID = @id AND AnonID = @anonId;
						";

						using (MySqlCommand alreadyCmd = new MySqlCommand(alreadySql, con, tx))
						{
							alreadyCmd.Parameters.AddWithValue("@id", questionId);
							alreadyCmd.Parameters.AddWithValue("@anonId", anonId);
							int alreadyCount = Convert.ToInt32(alreadyCmd.ExecuteScalar());
							if (alreadyCount > 0)
							{
								tx.Rollback();
								result.success = false;
								result.message = "You have already voted on this question.";
								result.hasNext = false;
								return result;
							}
						}

						string insertVote = @"
							INSERT INTO AdminQuestionsVotes (AdminQuestionID, AnonID, IsYes)
							VALUES (@id, @anonId, @isYes);
						";

						using (MySqlCommand cmd = new MySqlCommand(insertVote, con, tx))
						{
							cmd.Parameters.AddWithValue("@id", questionId);
							cmd.Parameters.AddWithValue("@anonId", anonId);
							cmd.Parameters.AddWithValue("@isYes", voteRight);
							cmd.ExecuteNonQuery();
						}

						string updateSql = voteRight
							? "UPDATE AdminQuestions SET YesVotes = YesVotes + 1 WHERE AdminQuestionID = @id"
							: "UPDATE AdminQuestions SET NoVotes = NoVotes + 1 WHERE AdminQuestionID = @id";

						using (MySqlCommand updateCmd = new MySqlCommand(updateSql, con, tx))
						{
							updateCmd.Parameters.AddWithValue("@id", questionId);
							updateCmd.ExecuteNonQuery();
						}

						tx.Commit();
					}
				}

				result.success = true;
				result.message = "Vote recorded.";
				skippedIds.Remove(questionId);
			}
			catch (MySqlException)
			{
				result.success = false;
				result.message = "Vote could not be recorded.";
			}

			try
			{
				result.nextQuestion = GetNextVoteQuestionForAnon(anonId, skippedIds);
				result.hasNext = result.nextQuestion != null;
			}
			catch
			{
				result.nextQuestion = null;
				result.hasNext = false;
			}

			return result;
		}

		[WebMethod(EnableSession = true)]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public BasicResult VoteSuggestion(int suggestionId, bool isUpvote)
		{
			BasicResult result = new BasicResult();

			if (!IsLoggedInSession())
			{
				result.success = false;
				result.message = "You must be logged in to vote.";
				return result;
			}

			if (!IsVerifiedSession())
			{
				result.success = false;
				result.message = "Access Denied";
				return result;
			}

			string anonId = EnsureAnonId();

			try
			{
				using (MySqlConnection con = new MySqlConnection(getConString()))
				{
					con.Open();

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

		[WebMethod]
		[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
		public object GetDashboardStats()
		{
			int uniqueVoters = 0;
			int totalQuestions = 0;
			int totalActivity = 0;
			string topConcernLabel = "—";
			int topConcernCount = 0;
			List<object> topics = new List<object>();

			try
			{
				using (MySqlConnection conn = new MySqlConnection(getConString()))
				{
					conn.Open();

					// Employees who have voted at least once on admin questions
					string sqlUniqueVoters = "SELECT COUNT(DISTINCT AnonID) FROM AdminQuestionsVotes;";
					using (var cmd = new MySqlCommand(sqlUniqueVoters, conn))
					{
						uniqueVoters = Convert.ToInt32(cmd.ExecuteScalar());
					}

					// Total admin questions (Vote Here questions)
					string sqlTotalQuestions = "SELECT COUNT(*) FROM AdminQuestions;";
					using (var cmd = new MySqlCommand(sqlTotalQuestions, conn))
					{
						totalQuestions = Convert.ToInt32(cmd.ExecuteScalar());
					}

					// Total votes submitted
					string sqlTotalActivity = "SELECT COUNT(*) FROM AdminQuestionsVotes;";
					using (var cmd = new MySqlCommand(sqlTotalActivity, conn))
					{
						totalActivity = Convert.ToInt32(cmd.ExecuteScalar());
					}

					// Top concern = question with most "No" votes (biggest concern)
					string sqlTopConcern = @"
						SELECT QuestionText, NoVotes
						FROM AdminQuestions
						WHERE NoVotes > 0
						ORDER BY NoVotes DESC
						LIMIT 1;
					";
					using (var cmd = new MySqlCommand(sqlTopConcern, conn))
					using (var rdr = cmd.ExecuteReader())
					{
						if (rdr.Read())
						{
							topConcernLabel = rdr["QuestionText"].ToString();
							topConcernCount = Convert.ToInt32(rdr["NoVotes"]);
						}
					}

					// Trending = top 3 questions by total votes (Yes + No), with % of total activity
					string sqlTopics = @"
						SELECT QuestionText, (YesVotes + NoVotes) AS VoteCount
						FROM AdminQuestions
						ORDER BY (YesVotes + NoVotes) DESC, CreatedDate DESC
						LIMIT 3;
					";
					using (var cmd = new MySqlCommand(sqlTopics, conn))
					using (var rdr = cmd.ExecuteReader())
					{
						while (rdr.Read())
						{
							int voteCount = Convert.ToInt32(rdr["VoteCount"]);
							int pct = totalActivity > 0 ? (int)Math.Round((voteCount * 100.0) / totalActivity) : 0;
							topics.Add(new
							{
								name = rdr["QuestionText"].ToString(),
								pct = pct,
								icon = "•",
								color = "#3498DB"
							});
						}
					}
				}
			}
			catch
			{
			}

			// Completion = % of possible votes (each voter × each question) that have been cast
			int possibleVotes = totalQuestions * (uniqueVoters > 0 ? uniqueVoters : 1);
			int completionPercent = possibleVotes > 0 ? (int)Math.Min(100, Math.Round((totalActivity * 100.0) / possibleVotes)) : 0;

			return new
			{
				requiredAnswered = uniqueVoters,
				stillNeedToAnswer = totalQuestions,
				totalActivity = totalActivity,
				completionPercent = completionPercent,
				topConcern = new { label = topConcernLabel, count = topConcernCount },
				topics = topics
			};
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
