/**
 * Shared configuration and small helpers for the BlastScale k6 scripts.
 *
 * Everything here runs inside k6 only (it imports k6 modules); the puzzle engine lives in
 * engine.js so that it can also be verified with Node.
 */
import http from 'k6/http';
import { check } from 'k6';

/** Base URL of the API under test. docker compose presets http://nginx:8080 for the k6 service. */
export const BASE_URL = (__ENV.BASE_URL || 'http://localhost:8080').replace(/\/+$/, '');

/** Prefix of every versioned endpoint. */
export const API = `${BASE_URL}/api/v1`;

/** Per-request timeout; the nginx proxy_read_timeout is 30s, so anything slower is already lost. */
export const REQUEST_TIMEOUT = __ENV.REQUEST_TIMEOUT || '30s';

/** Header carrying the client generated key that makes mutating calls safe to retry. */
export const IDEMPOTENCY_KEY_HEADER = 'Idempotency-Key';

/** Reads an integer environment variable; blank or non-numeric values fall back to the default. */
export function envInt(name, fallback) {
  const raw = __ENV[name];
  if (raw === undefined || raw === null || raw === '') {
    return fallback;
  }
  const value = parseInt(raw, 10);
  return Number.isFinite(value) ? value : fallback;
}

/** Reads a boolean environment variable: 1/true/yes/on are true, 0/false/no/off are false. */
export function envBool(name, fallback) {
  const raw = __ENV[name];
  if (raw === undefined || raw === null || raw === '') {
    return fallback;
  }
  return ['1', 'true', 'yes', 'on'].indexOf(String(raw).toLowerCase()) !== -1;
}

/** Reads a string environment variable with a default. */
export function envString(name, fallback) {
  const raw = __ENV[name];
  return raw === undefined || raw === null || raw === '' ? fallback : String(raw);
}

/**
 * Parses a ramp profile such as "30s:100,1m:500,2m:1000,30s:0" into k6 stages
 * (duration:targetVUs pairs separated by commas). Returns null when the input is empty.
 */
export function parseStages(raw) {
  if (!raw) {
    return null;
  }
  const stages = [];
  for (const part of String(raw).split(',')) {
    const pair = part.trim().split(':');
    if (pair.length !== 2) {
      throw new Error(`Invalid stage "${part}", expected <duration>:<targetVUs>, e.g. 1m:500`);
    }
    const target = parseInt(pair[1], 10);
    if (!Number.isFinite(target) || target < 0) {
      throw new Error(`Invalid VU target in stage "${part}"`);
    }
    stages.push({ duration: pair[0].trim(), target });
  }
  return stages;
}

/** RFC 4122 version 4 UUID built from Math.random: no jslib download, no crypto dependency. */
export function uuidv4() {
  let out = '';
  for (let i = 0; i < 36; i++) {
    if (i === 8 || i === 13 || i === 18 || i === 23) {
      out += '-';
    } else if (i === 14) {
      out += '4';
    } else if (i === 19) {
      out += ((Math.random() * 4) | 8).toString(16); // variant bits 10xx
    } else {
      out += ((Math.random() * 16) | 0).toString(16);
    }
  }
  return out;
}

/** JSON request headers with the bearer token (when logged in) and any extra headers merged in. */
export function jsonHeaders(token, extra) {
  const headers = { 'Content-Type': 'application/json', Accept: 'application/json' };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return Object.assign(headers, extra || {});
}

/**
 * Response callbacks deciding which HTTP statuses k6 counts as successful for `http_req_failed`.
 * Expected business rejections (no lives, locked level, reward already claimed, anti-cheat 422)
 * must not pollute the failure rate; anything else is a real failure.
 */
export const expect = {
  ok: http.expectedStatuses(200),
  okOrConflict: http.expectedStatuses(200, 409),
  levelStart: http.expectedStatuses(200, 403, 409),
  levelComplete: http.expectedStatuses(200, 422),
};

/**
 * Builds the k6 request params for one call: headers, a low-cardinality `name` tag (so metrics
 * are grouped per endpoint instead of per level number) and the expected statuses.
 */
export function requestParams(name, token, expected, extraHeaders) {
  return {
    headers: jsonHeaders(token, extraHeaders),
    tags: { name },
    responseCallback: expected || expect.ok,
    timeout: REQUEST_TIMEOUT,
  };
}

/**
 * Parses the JSON body and records two checks: the status is one of the expected ones and the
 * body is JSON. Returns {ok, status, body, code}; `code` is the ApiError code of error bodies.
 */
export function checkJson(res, label, expectedStatuses) {
  const expected = expectedStatuses && expectedStatuses.length ? expectedStatuses : [200];
  let body = null;
  if (res.body) {
    try {
      body = res.json();
    } catch (e) {
      body = null;
    }
  }
  const statusOk = expected.indexOf(res.status) !== -1;
  const ok = check(res, {
    [`${label}: status ${expected.join('/')}`]: () => statusOk,
    [`${label}: JSON body`]: () => body !== null && typeof body === 'object',
  });
  return {
    ok,
    status: res.status,
    body,
    code: body && typeof body.code === 'string' ? body.code : null,
  };
}

/** Seconds until the current fixed rate-limit window (one minute) rolls over, plus jitter. */
export function secondsUntilNextMinute() {
  const now = Date.now() / 1000;
  return 60 - (now % 60) + Math.random() * 3;
}

/** Trend statistics every script prints; p(50) is `med`, and p(95)/p(99) feed the thresholds. */
export const SUMMARY_TREND_STATS = ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'];
