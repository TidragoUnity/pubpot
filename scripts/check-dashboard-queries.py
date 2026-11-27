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


def grafana_variables(expr):
    """Grafana setzt $__interval selbst ein (im Panel mindestens der Wert von „interval“). Für die Prüfung reicht 1d.

    „offset -$__interval“ (Tagesbalken, E-059) zählt ab dem Zeitpunkt des Punkts nach vorn. Abgefragt wird hier aber
    nur „jetzt“, und nach vorn gibt es noch keine Daten. Deshalb fällt der Offset für die Prüfung weg.
    """
    return expr.replace(" offset -$__interval", "").replace("$__interval", "1d")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    dashboard_file, base, *must_have_data = sys.argv[1:]
    with open(dashboard_file, encoding="utf-8") as f:
        dashboard = json.load(f)

    failed = False
    for panel in dashboard["panels"]:
        for target in panel.get("targets", []):
            result = query(base, grafana_variables(target["expr"]))
            if result.get("status") != "success":
                print(f"  FEHLER Panel „{panel['title']}“: {result.get('error')}")
                failed = True
            elif panel["title"] in must_have_data and not result["data"]["result"]:
                print(f"  FEHLER Panel „{panel['title']}“ liefert keine Daten: {target['expr']}")
                failed = True

    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
