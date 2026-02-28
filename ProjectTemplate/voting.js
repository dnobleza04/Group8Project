let currentVoteQuestion = null;
let isSubmittingVote = false;
let dragStartX = null;
const dragThreshold = 80;
let pendingConfirmationMessage = "";
let myLeftVoteCount = 0;
let mySkipCount = 0;
let myRightVoteCount = 0;

function showVoteError(msg) {
    pendingConfirmationMessage = "";
    $("#confirmMsg").hide().text("");
    $("#errorMsg").show().text(msg);
}

function showVoteConfirmation(msg) {
    pendingConfirmationMessage = msg || "";
    $("#errorMsg").hide().text("");
    $("#confirmMsg").show().text(msg);
}

function setVoteButtonsEnabled(enabled) {
    $("#voteLeftBtn, #voteRightBtn, #skipBtn").prop("disabled", !enabled);
}

function renderVoteQuestion() {
    if (!currentVoteQuestion) {
        $("#progressText").text("No more questions to vote on.");
        $("#questionCard").html("<h3 style='margin:0;'>All caught up.</h3><div class='vote-done-badge'>You have voted on every question</div>");
        setVoteButtonsEnabled(false);
        $("#errorMsg").hide().text("");
        if (pendingConfirmationMessage) {
            $("#confirmMsg").show().text(pendingConfirmationMessage);
            pendingConfirmationMessage = "";
        } else {
            $("#confirmMsg").hide().text("");
        }
        return;
    }

    $("#progressText").text("");
    $("#questionCard").html(`
        <div style="display:flex; flex-direction:column; min-height:170px;">
            <div style="display:flex; align-items:center; justify-content:center; gap:8px;">
                <h3 style="margin:0;">${escapeHtml(currentVoteQuestion.QuestionText)}</h3>
            </div>

            <div style="margin-top:auto; font-size:13px; color:#666; text-align:center;">
                Left: ${myLeftVoteCount} | Skip: ${mySkipCount} | Right: ${myRightVoteCount}
            </div>
        </div>
    `);

    setVoteButtonsEnabled(true);
    $("#errorMsg").hide().text("");
    if (pendingConfirmationMessage) {
        $("#confirmMsg").show().text(pendingConfirmationMessage);
        pendingConfirmationMessage = "";
    } else {
        $("#confirmMsg").hide().text("");
    }
}

function loadNextVoteQuestion() {
    $.ajax({
        type: "POST",
        url: apiUrl("GetNextVoteQuestion"),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (res) {
            if (!res || typeof res.d === "undefined") {
                showVoteError("Unable to load question.");
                return;
            }

            currentVoteQuestion = res.d;
            renderVoteQuestion();
        },
        error: function () {
            showVoteError("Unable to load question.");
        }
    });
}

function submitQuestionVote(voteRight) {
    if (!currentVoteQuestion || isSubmittingVote) {
        return;
    }

    isSubmittingVote = true;
    setVoteButtonsEnabled(false);

    $.ajax({
        type: "POST",
        url: apiUrl("VoteQuestion"),
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify({
            questionId: currentVoteQuestion.QuestionID,
            voteRight: voteRight
        }),
        dataType: "json",
        success: function (res) {
            isSubmittingVote = false;

            if (!res || !res.d) {
                showVoteError("Vote failed.");
                setVoteButtonsEnabled(true);
                return;
            }

            const data = res.d;

            if (!data.success) {
                if ((data.message || "").includes("logged")) {
                    redirectToLogin();
                    return;
                }
                if ((data.message || "") === "Access Denied") {
                    showAccessDenied();
                    return;
                }

                showVoteError(data.message || "Vote failed.");
                setVoteButtonsEnabled(true);
                return;
            }

            if (voteRight) {
                myRightVoteCount += 1;
            } else {
                myLeftVoteCount += 1;
            }

            showVoteConfirmation("Vote recorded.");
            currentVoteQuestion = data.nextQuestion || null;
            renderVoteQuestion();
        },
        error: function () {
            isSubmittingVote = false;
            showVoteError("Vote failed.");
            setVoteButtonsEnabled(true);
        }
    });
}

function skipCurrentQuestion() {
    if (!currentVoteQuestion || isSubmittingVote) {
        return;
    }

    isSubmittingVote = true;
    setVoteButtonsEnabled(false);

    $.ajax({
        type: "POST",
        url: apiUrl("SkipQuestion"),
        contentType: "application/json; charset=utf-8",
        data: JSON.stringify({
            questionId: currentVoteQuestion.QuestionID
        }),
        dataType: "json",
        success: function (res) {
            isSubmittingVote = false;

            if (!res || !res.d) {
                showVoteError("Skip failed.");
                setVoteButtonsEnabled(true);
                return;
            }

            const data = res.d;

            if (!data.success) {
                if ((data.message || "") === "Access Denied") {
                    showAccessDenied();
                    return;
                }
                if ((data.message || "").includes("logged")) {
                    redirectToLogin();
                    return;
                }

                showVoteError(data.message || "Skip failed.");
                setVoteButtonsEnabled(true);
                return;
            }

            showVoteConfirmation("Question skipped.");
            mySkipCount += 1;
            currentVoteQuestion = data.nextQuestion || null;
            renderVoteQuestion();
        },
        error: function () {
            isSubmittingVote = false;
            showVoteError("Skip failed.");
            setVoteButtonsEnabled(true);
        }
    });
}

function bindSwipeVoting() {
    const card = document.getElementById("questionCard");
    if (!card) return;

    const onPointerDown = function (x) {
        dragStartX = x;
    };

    const onPointerUp = function (x) {
        if (dragStartX === null) return;

        const dx = x - dragStartX;
        dragStartX = null;

        if (Math.abs(dx) < dragThreshold) {
            return;
        }

        if (dx > 0) {
            submitQuestionVote(true);
        } else {
            submitQuestionVote(false);
        }
    };

    card.addEventListener("mousedown", function (e) {
        onPointerDown(e.clientX);
    });

    card.addEventListener("mouseup", function (e) {
        onPointerUp(e.clientX);
    });

    card.addEventListener("touchstart", function (e) {
        if (e.touches && e.touches.length > 0) {
            onPointerDown(e.touches[0].clientX);
        }
    }, { passive: true });

    card.addEventListener("touchend", function (e) {
        if (e.changedTouches && e.changedTouches.length > 0) {
            onPointerUp(e.changedTouches[0].clientX);
        }
    }, { passive: true });
}

function initVotingUI() {
    myLeftVoteCount = 0;
    mySkipCount = 0;
    myRightVoteCount = 0;

    $("#voteLeftBtn").on("click", function () { submitQuestionVote(false); });
    $("#voteRightBtn").on("click", function () { submitQuestionVote(true); });
    $("#skipBtn").on("click", function () { skipCurrentQuestion(); });

    bindSwipeVoting();
    loadNextVoteQuestion();
}