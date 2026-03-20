#!/usr/bin/env python3
"""
Simple test client for headless RPC Unix socket.
Usage:
  python3 scripts/test_rpc.py ping
  python3 scripts/test_rpc.py clear_entities --auth <auth>
  python3 scripts/test_rpc.py shutdown --auth <auth>

Defaults to socket /var/run/hass-agent.sock or HASS_AGENT_RPC_SOCKET env var.
"""
import sys
import json
import socket
import re
import argparse
import os

parser = argparse.ArgumentParser()
parser.add_argument('cmd', choices=['ping','clear_entities','shutdown'])
parser.add_argument('--auth', '-a', default='')
parser.add_argument('--socket', '-s', default=os.environ.get('HASS_AGENT_RPC_SOCKET','/var/run/hass-agent.sock'))
args = parser.parse_args()

payload = {'cmd': args.cmd}
if args.auth:
    payload['auth'] = args.auth

b = json.dumps(payload).encode('utf-8')

try:
    if args.socket.startswith('tcp://'):
        m = re.match(r'^tcp://([^:]+):(\d+)$', args.socket)
        if not m:
            raise ValueError('Invalid tcp socket format')
        host = m.group(1)
        port = int(m.group(2))
        client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client.connect((host, port))
        client.sendall(b)
        data = client.recv(8192)
        print('Response:', data.decode('utf-8'))
        client.close()
    else:
        client = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        client.connect(args.socket)
        client.sendall(b)
        data = client.recv(8192)
        print('Response:', data.decode('utf-8'))
        client.close()
except Exception as e:
    print('Error:', e)
    sys.exit(1)
