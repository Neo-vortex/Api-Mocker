#!/bin/bash
# download-assets.sh
# Run this ONCE on a machine with internet access, from the ApiMocker project root.
# Works on Linux/macOS. Requires curl.

BASE="$(dirname "$0")/wwwroot/lib"

download() {
    local url=$1
    local dest=$2
    mkdir -p "$(dirname "$dest")"
    printf "Downloading %s..." "$(basename "$dest")"
    if curl -sL "$url" -o "$dest"; then
        echo " OK ($(wc -c < "$dest") bytes)"
    else
        echo " FAILED"
    fi
}

# Bootstrap
download "https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"        "$BASE/bootstrap/css/bootstrap.min.css"
download "https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"   "$BASE/bootstrap/js/bootstrap.bundle.min.js"

# Bootstrap Icons
download "https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.css"   "$BASE/bootstrap-icons/css/bootstrap-icons.css"
download "https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/fonts/bootstrap-icons.woff"  "$BASE/bootstrap-icons/css/fonts/bootstrap-icons.woff"
download "https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/fonts/bootstrap-icons.woff2" "$BASE/bootstrap-icons/css/fonts/bootstrap-icons.woff2"

# CodeMirror core
download "https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/codemirror.min.css"   "$BASE/codemirror/css/codemirror.min.css"
download "https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/codemirror.min.js"    "$BASE/codemirror/js/codemirror.min.js"

# CodeMirror theme
download "https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/theme/dracula.min.css" "$BASE/codemirror/theme/dracula.min.css"

# CodeMirror Lua mode + addons
download "https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/mode/lua/lua.min.js"           "$BASE/codemirror/js/lua.min.js"
download "https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/addon/edit/matchbrackets.min.js"  "$BASE/codemirror/js/matchbrackets.min.js"
download "https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/addon/edit/closebrackets.min.js" "$BASE/codemirror/js/closebrackets.min.js"
download "https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/addon/comment/comment.min.js"    "$BASE/codemirror/js/comment.min.js"

# Chart.js
download "https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.1/chart.umd.min.js"         "$BASE/chartjs/chart.umd.min.js"

# SignalR
download "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js"  "$BASE/signalr/signalr.min.js"

echo ""
echo "Done. All assets saved to wwwroot/lib/"
echo "You can now copy the project to your offline environment."