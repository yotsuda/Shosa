"""Serves the repository to tools/vrm_video.html and keeps the frames it sends.

    py tools/frame_server.py OUT_FOLDER [--port 8378]

GET serves files from the repository (the page, the models, the .vrma). POST /frame/<n> writes
the body (a PNG) as OUT_FOLDER/<n>.png, POST /done writes OUT_FOLDER/done.txt with the body
(what the page says of the run), POST /log prints the body. Listens on 127.0.0.1 only.
"""
import argparse
import http.server
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("out", type=pathlib.Path)
    p.add_argument("--port", type=int, default=8378)
    args = p.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)

    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *a, **k):
            super().__init__(*a, directory=str(ROOT), **k)

        def do_POST(self):
            body = self.rfile.read(int(self.headers.get("Content-Length", 0)))
            parts = self.path.strip("/").split("/")
            if parts[0] == "frame" and len(parts) == 2 and parts[1].isdigit():
                (out / f"{int(parts[1]):05d}.png").write_bytes(body)
            elif parts[0] == "done":
                (out / "done.txt").write_bytes(body)
                print("done:", body.decode("utf-8", "replace"), flush=True)
            elif parts[0] == "log":
                print(body.decode("utf-8", "replace"), flush=True)
            else:
                self.send_error(404)
                return
            self.send_response(204)
            self.end_headers()

        def log_message(self, *a):
            pass

    print(f"serving {ROOT} on http://127.0.0.1:{args.port}, frames to {out}", flush=True)
    http.server.ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    sys.exit(main())
