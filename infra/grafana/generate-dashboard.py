#!/usr/bin/env python3
"""Generates infra/grafana/dashboards/blastscale.json ("dashboard as code").

Editing a 700-line JSON model by hand is error prone; this script keeps every PromQL query in
one readable place. Run it after changing metrics:  python3 infra/grafana/generate-dashboard.py
"""
import json
import os

APP = 'application="blastscale"'
DS = {"type": "prometheus", "uid": "prometheus"}

_panel_id = 0


def next_id():
    global _panel_id
    _panel_id += 1
    return _panel_id


def target(expr, legend):
    return {"datasource": DS, "expr": expr, "legendFormat": legend, "refId": chr(ord("A") + hash(legend) % 20)}


def timeseries(title, targets, x, y, w=8, h=8, unit=None, stacked=False, description=None):
    panel = {
        "id": next_id(), "type": "timeseries", "title": title, "datasource": DS,
        "gridPos": {"x": x, "y": y, "w": w, "h": h},
        "targets": [dict(t, refId=chr(ord("A") + i)) for i, t in enumerate(targets)],
        "fieldConfig": {"defaults": {"custom": {"lineWidth": 2, "fillOpacity": 12, "stacking": {"mode": "normal" if stacked else "none"}}},
                        "overrides": []},
        "options": {"legend": {"displayMode": "list", "placement": "bottom"}, "tooltip": {"mode": "multi"}},
    }
    if unit:
        panel["fieldConfig"]["defaults"]["unit"] = unit
    if description:
        panel["description"] = description
    return panel


def stat(title, expr, x, y, w=4, h=4, unit=None, thresholds=None, decimals=None, description=None):
    panel = {
        "id": next_id(), "type": "stat", "title": title, "datasource": DS,
        "gridPos": {"x": x, "y": y, "w": w, "h": h},
        "targets": [dict(target(expr, title), refId="A")],
        "options": {"reduceOptions": {"calcs": ["lastNotNull"]}, "colorMode": "value", "graphMode": "area"},
        "fieldConfig": {"defaults": {"thresholds": {"mode": "absolute", "steps": thresholds or [{"color": "green", "value": None}]}},
                        "overrides": []},
    }
    if unit:
        panel["fieldConfig"]["defaults"]["unit"] = unit
    if decimals is not None:
        panel["fieldConfig"]["defaults"]["decimals"] = decimals
    if description:
        panel["description"] = description
    return panel


def row(title, y):
    return {"id": next_id(), "type": "row", "title": title, "collapsed": False, "gridPos": {"x": 0, "y": y, "w": 24, "h": 1}}


def quantile(q, bucket_metric, window="1m", by=""):
    return f'histogram_quantile({q}, sum(rate({bucket_metric}{{{APP}}}[{window}])) by (le{by}))'


panels = [
    # ------------------------------------------------------------------ headline stats
    stat("Requests / sec", f'sum(rate(http_server_requests_seconds_count{{{APP}}}[1m]))', 0, 0, unit="reqps", decimals=1),
    stat("p95 latency", quantile(0.95, "http_server_requests_seconds_bucket"), 4, 0, unit="s", decimals=3,
         thresholds=[{"color": "green", "value": None}, {"color": "orange", "value": 0.2}, {"color": "red", "value": 0.5}]),
    stat("p99 latency", quantile(0.99, "http_server_requests_seconds_bucket"), 8, 0, unit="s", decimals=3,
         thresholds=[{"color": "green", "value": None}, {"color": "orange", "value": 0.5}, {"color": "red", "value": 1}]),
    stat("5xx rate", f'sum(rate(http_server_requests_seconds_count{{{APP},status=~"5.."}}[1m])) / sum(rate(http_server_requests_seconds_count{{{APP}}}[1m]))',
         12, 0, unit="percentunit", decimals=2, thresholds=[{"color": "green", "value": None}, {"color": "orange", "value": 0.001}, {"color": "red", "value": 0.01}]),
    stat("Redis cache hit rate", f'sum(rate(blastscale_cache_requests_total{{{APP},result="hit"}}[5m])) / sum(rate(blastscale_cache_requests_total{{{APP},result=~"hit|miss"}}[5m]))',
         16, 0, unit="percentunit", decimals=1, thresholds=[{"color": "red", "value": None}, {"color": "orange", "value": 0.5}, {"color": "green", "value": 0.8}]),
    stat("Outbox pending", f'max(blastscale_outbox_pending{{{APP}}})', 20, 0, unit="short", decimals=0,
         thresholds=[{"color": "green", "value": None}, {"color": "orange", "value": 100}, {"color": "red", "value": 1000}],
         description="Telemetry events waiting to be shipped to Elasticsearch. Grows when Elasticsearch is down; drains automatically."),

    # ------------------------------------------------------------------ traffic
    row("HTTP traffic", 4),
    timeseries("Requests / sec by endpoint", [target(f'topk(8, sum(rate(http_server_requests_seconds_count{{{APP}}}[1m])) by (uri))', "{{uri}}")], 0, 5, unit="reqps"),
    timeseries("Latency percentiles", [
        target(quantile(0.5, "http_server_requests_seconds_bucket"), "p50"),
        target(quantile(0.95, "http_server_requests_seconds_bucket"), "p95"),
        target(quantile(0.99, "http_server_requests_seconds_bucket"), "p99"),
    ], 8, 5, unit="s"),
    timeseries("Responses by status", [target(f'sum(rate(http_server_requests_seconds_count{{{APP}}}[1m])) by (status)', "{{status}}")], 16, 5, unit="reqps", stacked=True),

    # ------------------------------------------------------------------ gameplay
    row("Gameplay", 13),
    timeseries("Level completions / min", [target(f'sum(rate(blastscale_level_completion_total{{{APP}}}[5m])) by (result) * 60', "{{result}}")], 0, 14, unit="short",
               description="success = reward paid; replayed = idempotent retry answered from stored result; rejected = anti-cheat; failed = level lost"),
    timeseries("Anti-cheat rejections / min by validator", [target(f'sum(rate(blastscale_completion_rejected_total{{{APP}}}[5m])) by (validator) * 60', "{{validator}}")], 8, 14, unit="short"),
    timeseries("Reward pipeline duration", [
        target(quantile(0.5, "blastscale_reward_processing_duration_seconds_bucket", "5m"), "p50"),
        target(quantile(0.95, "blastscale_reward_processing_duration_seconds_bucket", "5m"), "p95"),
        target(quantile(0.99, "blastscale_reward_processing_duration_seconds_bucket", "5m"), "p99"),
    ], 16, 14, unit="s", description="validate -> replay -> reward -> persist, end to end"),
    timeseries("Level starts / min", [target(f'sum(rate(blastscale_level_start_total{{{APP}}}[5m])) * 60', "starts")], 0, 22, unit="short"),
    timeseries("Economy transactions / min", [target(f'sum(rate(blastscale_economy_transaction_total{{{APP}}}[5m])) by (resource, type) * 60', "{{resource}} {{type}}")], 8, 22, unit="short", stacked=True),
    timeseries("Idempotent replays & rate limits / min", [
        target(f'sum(rate(blastscale_idempotent_replay_total{{{APP}}}[5m])) by (scope) * 60', "replay {{scope}}"),
        target(f'sum(rate(blastscale_rate_limit_rejected_total{{{APP}}}[5m])) * 60', "rate limited"),
    ], 16, 22, unit="short"),

    # ------------------------------------------------------------------ infrastructure
    row("Infrastructure", 30),
    timeseries("Cache requests / sec", [target(f'sum(rate(blastscale_cache_requests_total{{{APP}}}[1m])) by (cache, result)', "{{cache}} {{result}}")], 0, 31, unit="reqps", stacked=True),
    timeseries("Outbox throughput / sec", [
        target(f'sum(rate(blastscale_outbox_published_total{{{APP}}}[1m]))', "published"),
        target(f'sum(rate(blastscale_outbox_failed_total{{{APP}}}[1m]))', "failed"),
        target(f'max(blastscale_outbox_pending{{{APP}}})', "pending"),
    ], 8, 31, unit="short"),
    timeseries("MySQL connection pool", [
        target(f'sum(hikaricp_connections_active{{{APP}}}) by (instance)', "active {{instance}}"),
        target(f'sum(hikaricp_connections_pending{{{APP}}}) by (instance)', "waiting {{instance}}"),
        target(f'max(hikaricp_connections_max{{{APP}}})', "max"),
    ], 16, 31, unit="short"),
    timeseries("JVM heap used", [target(f'sum(jvm_memory_used_bytes{{{APP},area="heap"}}) by (instance)', "{{instance}}")], 0, 39, unit="bytes"),
    timeseries("CPU usage", [target(f'process_cpu_usage{{{APP}}}', "{{instance}}")], 8, 39, unit="percentunit"),
    timeseries("Tomcat threads busy", [target(f'tomcat_threads_busy_threads{{{APP}}}', "{{instance}}")], 16, 39, unit="short"),
]

dashboard = {
    "uid": "blastscale-overview",
    "title": "BlastScale Overview",
    "tags": ["blastscale", "gameplay"],
    "timezone": "browser",
    "schemaVersion": 39,
    "version": 1,
    "refresh": "5s",
    "time": {"from": "now-15m", "to": "now"},
    "editable": True,
    "graphTooltip": 1,
    "panels": panels,
    "templating": {"list": []},
    "annotations": {"list": []},
}

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "dashboards", "blastscale.json")
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w") as f:
    json.dump(dashboard, f, indent=2)
print(f"wrote {out} with {len(panels)} panels")
