import { createReadStream, existsSync, readFileSync, statSync } from "node:fs";
import { createServer } from "node:http";
import { extname, isAbsolute, join, relative, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const browserRoot = fileURLToPath(new URL("./", import.meta.url));
const contentTypes = Object.freeze({
  ".css": "text/css; charset=utf-8",
  ".data": "application/octet-stream",
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".symbols.json": "application/json; charset=utf-8",
  ".wasm": "application/wasm"
});

export function createHarnessServer(options = {}) {
  const harnessRoot = resolve(options.harnessRoot || browserRoot);
  const buildRoot = options.buildRoot ? resolve(options.buildRoot) : null;
  const catalogPath = resolve(
    options.catalogPath || join(harnessRoot, "commands.generated.json"));
  if (!existsSync(catalogPath)) {
    throw new Error(`Command catalog not found: ${catalogPath}`);
  }
  if (buildRoot && !existsSync(join(buildRoot, "index.html"))) {
    throw new Error(`Unity WebGL build not found: ${buildRoot}`);
  }

  return createServer((request, response) => {
    const url = new URL(request.url || "/", "http://localhost");
    if (url.pathname === "/") {
      response.writeHead(302, { location: "/harness.html" });
      response.end();
      return;
    }
    if (url.pathname === "/harness-config.generated.json") {
      sendJson(response, {
        viewer_path: buildRoot ? "/viewer/index.html" : "/mock-viewer.html"
      });
      return;
    }
    if (url.pathname === "/commands.generated.json") {
      sendFile(response, catalogPath);
      return;
    }

    const viewerPrefix = "/viewer/";
    if (url.pathname.startsWith(viewerPrefix)) {
      if (!buildRoot) {
        notFound(response);
        return;
      }
      const viewerPath = url.pathname.slice(viewerPrefix.length);
      if (viewerPath === "index.html") {
        sendViewerIndex(response, join(buildRoot, "index.html"));
      } else {
        sendResolvedFile(response, buildRoot, viewerPath);
      }
      return;
    }

    sendResolvedFile(response, harnessRoot, url.pathname.slice(1));
  });
}

export function injectLoopbackViewerConfiguration(source) {
  if (typeof source !== "string" || !source) {
    throw new Error("The Unity WebGL index is empty.");
  }
  const head = /<head(?:\s[^>]*)?>/i.exec(source);
  if (!head) {
    throw new Error("The Unity WebGL index has no head element.");
  }
  const insertion = head.index + head[0].length;
  const bootstrap = `
    <script>
      (function () {
        "use strict";
        var hostname = window.location.hostname;
        if (hostname !== "localhost" && hostname !== "127.0.0.1" &&
            hostname !== "[::1]") {
          throw new Error("The local Web Viewer harness requires a loopback origin.");
        }
        var existing = window.deucarianWebViewerConfig ||
          window.DeucarianWebViewerConfig || {};
        window.deucarianWebViewerConfig = Object.assign({}, existing, {
          parentOrigin: window.location.origin
        });
      }());
    </script>`;
  return source.slice(0, insertion) + bootstrap + source.slice(insertion);
}

function sendViewerIndex(response, path) {
  if (!existsSync(path) || !statSync(path).isFile()) {
    notFound(response);
    return;
  }
  let body;
  try {
    body = injectLoopbackViewerConfiguration(readFileSync(path, "utf8"));
  } catch (error) {
    response.writeHead(500, {
      "cache-control": "no-store",
      "content-type": "text/plain; charset=utf-8"
    });
    response.end(error.message);
    return;
  }
  response.writeHead(200, {
    "cache-control": "no-store",
    "content-length": Buffer.byteLength(body),
    "content-type": "text/html; charset=utf-8"
  });
  response.end(body);
}

function sendJson(response, value) {
  const body = JSON.stringify(value);
  response.writeHead(200, {
    "cache-control": "no-store",
    "content-length": Buffer.byteLength(body),
    "content-type": "application/json; charset=utf-8"
  });
  response.end(body);
}

function sendResolvedFile(response, root, requestPath) {
  let decoded;
  try {
    decoded = decodeURIComponent(requestPath);
  } catch (_) {
    notFound(response);
    return;
  }
  const target = resolve(root, decoded);
  const pathFromRoot = relative(root, target);
  if (pathFromRoot.startsWith(".." + sep) || pathFromRoot === ".." ||
      isAbsolute(pathFromRoot)) {
    notFound(response);
    return;
  }
  sendFile(response, target);
}

function sendFile(response, path) {
  if (!existsSync(path) || !statSync(path).isFile()) {
    notFound(response);
    return;
  }
  const extension = path.endsWith(".symbols.json")
    ? ".symbols.json"
    : extname(path).toLowerCase();
  response.writeHead(200, {
    "cache-control": "no-store",
    "content-type": contentTypes[extension] || "application/octet-stream"
  });
  createReadStream(path).pipe(response);
}

function notFound(response) {
  response.writeHead(404, { "content-type": "text/plain; charset=utf-8" });
  response.end("Not found");
}

export function parseArguments(values) {
  const result = { port: 8080 };
  for (let index = 0; index < values.length; index += 1) {
    const key = values[index];
    const value = values[index + 1];
    if (key === "--build") result.buildRoot = value;
    else if (key === "--catalog") result.catalogPath = value;
    else if (key === "--port") result.port = Number(value);
    else throw new Error(`Unknown argument: ${key}`);
    index += 1;
  }
  if (!Number.isInteger(result.port) || result.port < 1 || result.port > 65535) {
    throw new Error("The port must be an integer from 1 through 65535.");
  }
  return result;
}

if (process.argv[1] &&
    import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const configuration = parseArguments(process.argv.slice(2));
  const server = createHarnessServer(configuration);
  server.listen(configuration.port, "127.0.0.1", () => {
    process.stdout.write(
      `Local Web Viewer harness: http://localhost:${configuration.port}/harness.html\n`);
  });
}
