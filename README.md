# Sensor Data Api Collector
Goal of this repo is to collect data like temperatur, humidity etc. as a source for the master thesis, where this data is used to detect heat islands.

## Connected Data Sources

- [ ][Sensor Community](https://github.com/opendata-stuttgart/meta/wiki/APIs)
  - either via API calls all 5 sec, or maybe better: get csv from archive and import once a day

- [ ][Netatmo](https://dev.netatmo.com/apidocumentation/weather)
  - either job all 10 secs, or maybe via historic data endpoint
  - https://dev.netatmo.com/apidocumentation/weather#getpublicdata