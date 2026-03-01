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

function hasNoMoreQuestions() {
    return !currentVoteQuestion || !currentVoteQuestion.QuestionID;
}

function renderVoteQuestion() {
    if (hasNoMoreQuestions()) {
        currentVoteQuestion = null;
        $("#progressText").text("No more questions to vote on.");
        $("#questionCardInner").html(
            "<div class='vote-done-state'>" +
            "<h3 class='vote-done-title'>All caught up.</h3>" +
            "<p class='vote-done-badge'>You have voted on every question</p>" +
            "</div>"
        );
        $("#voteStats").text("Yes: " + myRightVoteCount + " | Skip: " + mySkipCount + " | No: " + myLeftVoteCount).show();
        setVoteButtonsEnabled(false);
        $("#errorMsg").hide().text("");
        if (pendingConfirmationMessage) {
            $("#confirmMsg").show().text(pendingConfirmationMessage);
            pendingConfirmationMessage = "";
        } else {
            $("#confirmMsg").hide().text("");
        }
        notifyVoteRequirementKnown();
        return;
    }

    $("#progressText").text("");
    $("#questionCardInner").html(`
        <div class="swipe-track" id="swipeTrack">
            <div class="swipe-card-inner" id="swipeCardInner">
                <div style="display:flex; flex-direction:column; flex:1; align-items:center; justify-content:center;">
                    <h3 style="margin:0;">${escapeHtml(currentVoteQuestion.QuestionText)}</h3>
                </div>
            </div>
            <span class="swipe-hint swipe-hint-left" id="swipeHintLeft">No</span>
            <span class="swipe-hint swipe-hint-right" id="swipeHintRight">Yes</span>
        </div>
    `);
    $("#voteStats").text("Yes: " + myRightVoteCount + " | Skip: " + mySkipCount + " | No: " + myLeftVoteCount).show();

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

            var next = res.d;
            currentVoteQuestion = (next && next.QuestionID) ? next : null;
            renderVoteQuestion();
            if (hasNoMoreQuestions()) notifyVoteRequirementKnown();
        },
        error: function () {
            showVoteError("Unable to load question.");
        }
    });
}

function notifyVoteRequirementKnown() {
    var completed = hasNoMoreQuestions();
    try {
        if (completed) sessionStorage.setItem("voteRequirementComplete", "1");
    } catch (e) {}
    if (typeof window.onVoteRequirementKnown === "function") {
        window.onVoteRequirementKnown(completed);
    }
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
            var next = data.nextQuestion;
            currentVoteQuestion = (next && next.QuestionID) ? next : null;
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
            var next = data.nextQuestion;
            currentVoteQuestion = (next && next.QuestionID) ? next : null;
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
    const card = document.getElementById("questionCardInner");
    if (!card) return;

    let isDragging = false;
    let currentX = 0;

    function getInner() {
        return document.getElementById("swipeCardInner");
    }
    function getHintLeft() { return document.getElementById("swipeHintLeft"); }
    function getHintRight() { return document.getElementById("swipeHintRight"); }

    function updateDragVisual(dx) {
        const inner = getInner();
        const hintL = getHintLeft();
        const hintR = getHintRight();
        if (!inner) return;
        var t = Math.min(Math.abs(dx) / dragThreshold, 1);
        var tilt = dx * 0.025;
        var scale = 1 + 0.02 * t;
        inner.style.transform = "translateX(" + dx + "px) rotate(" + tilt + "deg) scale(" + scale + ")";
        inner.classList.add("dragging");
        if (dx > 0) {
            inner.style.backgroundColor = "rgba(34, 197, 94, " + (0.18 * t) + ")";
            if (hintL) hintL.style.opacity = "0";
            if (hintR) hintR.style.opacity = t * 0.9;
        } else if (dx < 0) {
            inner.style.backgroundColor = "rgba(239, 68, 68, " + (0.18 * t) + ")";
            if (hintL) hintL.style.opacity = t * 0.9;
            if (hintR) hintR.style.opacity = "0";
        } else {
            inner.style.backgroundColor = "";
            if (hintL) hintL.style.opacity = "0";
            if (hintR) hintR.style.opacity = "0";
        }
    }

    function clearDragVisual() {
        const inner = getInner();
        const hintL = getHintLeft();
        const hintR = getHintRight();
        if (inner) {
            inner.style.transform = "";
            inner.style.backgroundColor = "";
            inner.classList.remove("dragging");
        }
        if (hintL) hintL.style.opacity = "0";
        if (hintR) hintR.style.opacity = "0";
    }

    function clearDragCursor() {
        document.body.classList.remove("swipe-dragging");
        document.body.style.cursor = "";
    }

    function onPointerDown(x, isTouch) {
        if (!getInner() || isSubmittingVote) return;
        dragStartX = x;
        isDragging = true;
        currentX = x;
        if (!isTouch) {
            document.body.classList.add("swipe-dragging");
            document.body.style.cursor = "grabbing";
        }

        function onMove(x) {
            if (dragStartX === null) return;
            currentX = x;
            var dx = x - dragStartX;
            updateDragVisual(dx);
        }
        function onUp(x) {
            if (dragStartX === null) return;
            var dx = x - dragStartX;
            dragStartX = null;
            isDragging = false;
            clearDragCursor();

            document.removeEventListener("mousemove", onMouseMove);
            document.removeEventListener("mouseup", onMouseUp);
            document.removeEventListener("touchmove", onTouchMove, { passive: true });
            document.removeEventListener("touchend", onTouchEnd, { passive: true });

            if (Math.abs(dx) < dragThreshold) {
                clearDragVisual();
                return;
            }

            var voteYes = dx > 0;
            var inner = getInner();
            if (!inner) return;

            inner.classList.remove("dragging");
            inner.classList.add("swipe-fly-away");
            void inner.offsetHeight;
            requestAnimationFrame(function () {
                inner.style.transform = voteYes ? "translateX(120%) rotate(12deg)" : "translateX(-120%) rotate(-12deg)";
                inner.style.opacity = "0";
            });

            var done = false;
            function finish() {
                if (done) return;
                done = true;
                inner.removeEventListener("transitionend", onFlyEnd);
                submitQuestionVote(voteYes);
            }
            function onFlyEnd(e) {
                if (e.target !== inner) return;
                if (e.propertyName === "transform" || e.propertyName === "opacity") finish();
            }
            inner.addEventListener("transitionend", onFlyEnd);
            setTimeout(finish, 450);
        }

        var onMouseMove = function (e) { onMove(e.clientX); };
        var onMouseUp = function (e) { onUp(e.clientX); };
        function onTouchMove(e) {
            if (e.touches && e.touches.length > 0) onMove(e.touches[0].clientX);
        }
        function onTouchEnd(e) {
            if (e.changedTouches && e.changedTouches.length > 0) onUp(e.changedTouches[0].clientX);
        }

        document.addEventListener("mousemove", onMouseMove);
        document.addEventListener("mouseup", onMouseUp);
        document.addEventListener("touchmove", onTouchMove, { passive: true });
        document.addEventListener("touchend", onTouchEnd, { passive: true });
    }

    function onPointerDownMouse(x) { onPointerDown(x, false); }
    function onPointerDownTouch(x) { onPointerDown(x, true); }

    card.addEventListener("mousedown", function (e) {
        if (e.button !== 0) return;
        var track = document.getElementById("swipeTrack");
        if (!track || !track.contains(e.target)) return;
        onPointerDownMouse(e.clientX);
    });

    card.addEventListener("touchstart", function (e) {
        if (e.touches && e.touches.length > 0) {
            var track = document.getElementById("swipeTrack");
            if (!track || !track.contains(e.target)) return;
            onPointerDownTouch(e.touches[0].clientX);
        }
    }, { passive: true });
}

function initVotingUI(options) {
    myLeftVoteCount = 0;
    mySkipCount = 0;
    myRightVoteCount = 0;

    $("#voteStats").text("Yes: 0 | Skip: 0 | No: 0").show();
    $("#voteLeftBtn").on("click", function () { submitQuestionVote(false); });
    $("#voteRightBtn").on("click", function () { submitQuestionVote(true); });
    $("#skipBtn").on("click", function () { skipCurrentQuestion(); });

    bindSwipeVoting();
    loadNextVoteQuestion();
}