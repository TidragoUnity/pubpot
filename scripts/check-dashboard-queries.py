"""Prüft jede Abfrage des Grafana-Dashboards gegen ein laufendes Prometheus (Smoke-Test, Schritt 7.4).

  python3 scripts/check-dashboard-queries.py <dashboard.json> <prometheus-url> [Panel-Titel, die Daten liefern müssen ...]

Fehler in PromQL (Tippfehler, falsche Klammern) fallen so schon in der CI auf und nicht erst als leeres Panel.
"""
import json
import sys
import urllib.parse
import urllib.request


def query(base, expr):
    url = f"{base}/api/v1/query?{urllib.parse.urlencode({'query': expr})}"
    try:
        with urllib.request.urlopen(url, timeout=10) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        return json.load(error)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    dashboard_file, base, *must_have_data = sys.argv[1:]
    with open(dashboard_file, encoding="utf-8") as f:
        dashboard = json.load(f)

    failed = False
    for panel in dashboard["panels"]:
        for target in panel.get("targets", []):
            result = query(base, target["expr"])
            if result.get("status") != "success":
                print(f"  FEHLER Panel „{panel['title']}“: {result.get('error')}")
                failed = True
            elif panel["title"] in must_have_data and not result["data"]["result"]:
                print(f"  FEHLER Panel „{panel['title']}“ liefert keine Daten: {target['expr']}")
                failed = True

    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
