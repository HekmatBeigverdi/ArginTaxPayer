const activityPath = "/api/identity/activity";
const activityInterval = 5_000;
const retryDelay = 15_000;
const interactionEvents = ["pointerdown", "keydown", "wheel", "touchmove"] as const;

export function trackSessionActivity(onExpired: () => void, setBlocked: (blocked: boolean) => void): () => void {
  let stopped = false;
  let checking = false;
  let activityQueued = false;
  let lastActivity = -Infinity;
  let deadlineTimer: number | undefined;
  let retryTimer: number | undefined;
  let activityTimer: number | undefined;

  function scheduleDeadline(expiresInMs: number) {
    window.clearTimeout(deadlineTimer);
    deadlineTimer = window.setTimeout(() => {
      deadlineTimer = undefined;
      setBlocked(true);
      void request("GET");
    }, Math.max(250, expiresInMs));
  }

  async function request(method: "GET" | "POST") {
    if (stopped || checking) return;
    window.clearTimeout(retryTimer);
    checking = true;
    try {
      const response = await fetch(activityPath, {
        method,
        credentials: "same-origin",
        cache: "no-store",
        keepalive: method === "POST",
        signal: AbortSignal.timeout(10_000),
      });
      if (stopped) return;
      if (response.status === 401) {
        setBlocked(true);
        stop();
        onExpired();
        return;
      }
      if (!response.ok) throw new Error("session_status_unavailable");
      const { expiresInMs } = await response.json() as { expiresInMs: number };
      if (!Number.isFinite(expiresInMs)) throw new Error("invalid_session_status");
      if (!stopped) {
        setBlocked(false);
        scheduleDeadline(expiresInMs);
      }
    } catch {
      if (!stopped) {
        if (deadlineTimer === undefined) setBlocked(true);
        retryTimer = window.setTimeout(() => void request("GET"), retryDelay);
      }
    } finally {
      checking = false;
      if (activityQueued && !stopped) {
        activityQueued = false;
        sendActivity();
      }
    }
  }

  function sendActivity() {
    activityTimer = undefined;
    if (checking) {
      activityQueued = true;
      return;
    }
    lastActivity = performance.now();
    void request("POST");
  }

  function onInteraction(event: Event) {
    if (!event.isTrusted || document.visibilityState !== "visible" || activityTimer !== undefined) return;
    const delay = activityInterval - (performance.now() - lastActivity);
    if (delay > 0) activityTimer = window.setTimeout(sendActivity, delay);
    else sendActivity();
  }

  function onVisibilityChange() {
    if (document.visibilityState === "visible") {
      void request("GET");
    } else if (activityTimer !== undefined) {
      window.clearTimeout(activityTimer);
      sendActivity();
    }
  }

  function stop() {
    stopped = true;
    window.clearTimeout(deadlineTimer);
    window.clearTimeout(retryTimer);
    window.clearTimeout(activityTimer);
    for (const event of interactionEvents) window.removeEventListener(event, onInteraction, true);
    document.removeEventListener("visibilitychange", onVisibilityChange);
  }

  for (const event of interactionEvents) {
    window.addEventListener(event, onInteraction, { capture: true, passive: true });
  }
  document.addEventListener("visibilitychange", onVisibilityChange);
  void request("GET");
  return stop;
}
