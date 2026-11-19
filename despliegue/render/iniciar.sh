#!/bin/bash
set -eu
port="${PORT:-10000}"
[[ "$port" =~ ^[0-9]+$ ]] && ((port>=1024 && port<=65535)) || { echo 'Puerto no válido.';exit 1; }
cat > /tmp/nginx.conf <<NGINX
pid /tmp/nginx.pid;
error_log /dev/stderr warn;
events { worker_connections 256; }
http {
 access_log off;
 client_body_temp_path /tmp/nginx-client;
 proxy_temp_path /tmp/nginx-proxy;
 server {
  listen ${port};
  client_max_body_size 6m;
  location / {
   proxy_pass http://127.0.0.1:8081;
   proxy_set_header Host \$http_host;
   proxy_set_header X-Forwarded-Proto https;
   proxy_set_header X-Forwarded-For "";
   proxy_set_header Forwarded "";
   proxy_read_timeout 120s;
  }
 }
}
NGINX
dotnet /app/Motherson.Api.dll & backend=$!
nginx -c /tmp/nginx.conf -g 'daemon off;' & proxy=$!
trap 'kill "$backend" "$proxy" 2>/dev/null || true' EXIT TERM INT
set +e
wait -n "$backend" "$proxy"
exit $?
