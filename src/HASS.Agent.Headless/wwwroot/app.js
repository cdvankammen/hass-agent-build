async function apiPost(path, body) {
  const resp = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
  return resp.text();
}

async function apiPostJson(path, body) {
  const resp = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });

  const text = await resp.text();
  if (!resp.ok) {
    throw new Error(text || resp.statusText);
  }

  return text ? JSON.parse(text) : {};
}

function Guid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    var r = Math.random() * 16 | 0;
    var v = c == 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

document.addEventListener('DOMContentLoaded', () => {
  const settingsEditor = document.getElementById('settingsEditor');
  const settingsStatus = document.getElementById('settingsStatus');
  let autosaveTimer = null;

  function setSettingsStatus(message, isError = false) {
    settingsStatus.textContent = message;
    settingsStatus.style.color = isError ? '#c00' : '#080';
  }

  async function loadCurrentSettings() {
    const r = await fetch('/settings');
    if (!r.ok) {
      throw new Error(await r.text());
    }

    const settings = await r.json();
    settingsEditor.value = JSON.stringify(settings, null, 2);
    setSettingsStatus('Loaded current settings snapshot.');
  }

  async function validateSettings() {
    const raw = settingsEditor.value.trim();
    const parsed = raw ? JSON.parse(raw) : {};
    const result = await apiPostJson('/settings/validate', parsed);
    if (result.valid) {
      setSettingsStatus('Settings are valid.');
      return { valid: true, parsed };
    }

    const errors = result.errors || {};
    const message = Object.entries(errors)
      .map(([key, value]) => `${key}: ${value}`)
      .join(' | ') || 'Settings validation failed.';
    setSettingsStatus(message, true);
    return { valid: false, parsed, errors };
  }

  async function saveCurrentSettings({ silent = false } = {}) {
    try {
      const { valid, parsed } = await validateSettings();
      if (!valid) return false;

      const res = await fetch('/settings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(parsed)
      });

      const text = await res.text();
      if (!res.ok || text !== 'ok') {
        throw new Error(text || res.statusText);
      }

      setSettingsStatus('Settings saved.');

      if (document.getElementById('autoApply').checked) {
        const applyResp = await fetch('/settings/apply', { method: 'POST' });
        const applyText = await applyResp.text();
        if (!applyResp.ok || applyText !== 'ok') {
          throw new Error(applyText || applyResp.statusText);
        }
      }

      if (!silent) {
        settingsEditor.value = JSON.stringify(parsed, null, 2);
      }

      return true;
    } catch (error) {
      setSettingsStatus(error.message, true);
      return false;
    }
  }

  document.getElementById('publishBtn').addEventListener('click', async () => {
    const res = await apiPost('/discovery/publish', {});
    alert('Publish: ' + res);
  });

  document.getElementById('clearBtn').addEventListener('click', async () => {
    const res = await apiPost('/discovery/clear', {});
    alert('Clear: ' + res);
  });

  document.getElementById('refreshCmds').addEventListener('click', async () => {
    const r = await fetch('/commands');
    const list = await r.json();
    const el = document.getElementById('commandsList');
    el.innerHTML = '';
    list.forEach(c => {
      const li = document.createElement('li');
      li.textContent = `${c.Id} - ${c.Name} `;
      const btn = document.createElement('button');
      btn.textContent = 'Run';
      btn.addEventListener('click', async () => {
        const response = await apiPostJson('/command', c);
        alert(`Command executed: ${response.success ? 'ok' : 'failed'}`);
      });
      li.appendChild(btn);
      el.appendChild(li);
    });
  });

  document.getElementById('refreshSensors').addEventListener('click', async () => {
    const r = await fetch('/sensors');
    const list = await r.json();
    const el = document.getElementById('sensorsList');
    el.innerHTML = '';
    list.forEach(s => {
      const li = document.createElement('li');
      li.textContent = `${s.Id} - ${s.Name} - ${s.State}`;
      el.appendChild(li);
    });
  });

  document.getElementById('runCustom').addEventListener('click', async () => {
    const name = document.getElementById('cmdName').value;
    const exec = document.getElementById('cmdExec').value;
    const payload = {
      Id: Guid(),
      Name: name,
      EntityType: 'custom',
      Command: exec,
      Args: '',
      Keys: [],
      RunAsLowIntegrity: false
    };
    const result = await apiPostJson('/command', payload);
    alert(`Custom command sent: ${result.success ? 'ok' : 'failed'}`);
  });

  document.getElementById('loadSettings').addEventListener('click', async () => {
    try {
      await loadCurrentSettings();
    } catch (error) {
      setSettingsStatus(error.message, true);
    }
  });

  document.getElementById('validateSettings').addEventListener('click', async () => {
    await validateSettings();
  });

  document.getElementById('saveSettings').addEventListener('click', async () => {
    await saveCurrentSettings({ silent: false });
  });

  document.getElementById('autoSave').addEventListener('change', () => {
    try {
      localStorage.setItem('hass_autosave', document.getElementById('autoSave').checked ? '1' : '0');
    } catch (e) {}
  });

  document.getElementById('autoApply').addEventListener('change', () => {
    try {
      localStorage.setItem('hass_autoapply', document.getElementById('autoApply').checked ? '1' : '0');
    } catch (e) {}
  });

  settingsEditor.addEventListener('input', () => {
    if (!document.getElementById('autoSave').checked) return;
    if (autosaveTimer) clearTimeout(autosaveTimer);
    autosaveTimer = setTimeout(async () => {
      await saveCurrentSettings({ silent: true });
    }, 800);
  });

  async function updatePlatformStatus() {
    try {
      const r = await fetch('/platform/status');
      const j = await r.json();
      const el = document.getElementById('platformStatus');
      el.textContent = `MPRIS(DBus): ${j.mprisDbus}, BlueZ(DBus): ${j.bluezDbus}, playerctl: ${j.playerctl}, bluetoothctl: ${j.btctl}, mediaEffective: ${j.effectiveMedia}`;
    } catch (e) { console.warn('platform status failed', e); }
  }

  async function updateNetworkStatus() {
    try {
      const r = await fetch('/network/status');
      const j = await r.json();
      const statusEl = document.getElementById('networkStatus');
      statusEl.textContent = `Bind host: ${j.bind_host}, all interfaces: ${j.listening_on_all_interfaces ? 'yes' : 'no'}, sensor state: ${j.network_state}, active interfaces: ${j.active_interfaces}/${j.total_interfaces}`;

      const listEl = document.getElementById('networkInterfaces');
      listEl.innerHTML = '';
      (j.interfaces || []).forEach(iface => {
        const li = document.createElement('li');
        const ipv4 = (iface.ipv4_addresses || []).join(', ') || 'no IPv4';
        const ipv6 = (iface.ipv6_addresses || []).join(', ') || 'no IPv6';
        li.textContent = `${iface.name} (${iface.status}) - ${ipv4} | ${ipv6}`;
        listEl.appendChild(li);
      });
    } catch (e) { console.warn('network status failed', e); }
  }

  async function updateMediaStatus() {
    try {
      const r = await fetch('/media/status');
      const j = await r.json();
      const el = document.getElementById('mediaStatus');
      if (!j.available) {
        el.textContent = 'Media backend unavailable.';
        return;
      }

      el.textContent = `Status: ${j.status}, title: ${j.title || 'n/a'}, artist: ${j.artist || 'n/a'}, album: ${j.album || 'n/a'}, position: ${j.position || '0'}`;
    } catch (e) {
      console.warn('media status failed', e);
    }
  }

  async function runMediaAction(path, label) {
    try {
      const resp = await fetch(path, { method: 'POST' });
      const text = await resp.text();
      alert(`${label}: ${resp.ok ? text : text || resp.statusText}`);
      await updateMediaStatus();
    } catch (e) {
      alert(`${label}: ${e.message}`);
    }
  }

  document.getElementById('mediaPlay').addEventListener('click', () => runMediaAction('/media/play', 'Play'));
  document.getElementById('mediaPause').addEventListener('click', () => runMediaAction('/media/pause', 'Pause'));
  document.getElementById('mediaPrevious').addEventListener('click', () => runMediaAction('/media/previous', 'Previous'));
  document.getElementById('mediaNext').addEventListener('click', () => runMediaAction('/media/next', 'Next'));

  async function updateBluetoothStatus() {
    try {
      const devicesResp = await fetch('/bluetooth/devices');
      const devicesJson = await devicesResp.json();
      const connectedResp = await fetch('/bluetooth/connected');
      const connectedJson = await connectedResp.json();

      const statusEl = document.getElementById('bluetoothStatus');
      const pairedDevices = Array.isArray(devicesJson) ? devicesJson : (devicesJson.devices || []);
      const connectedDevices = Array.isArray(connectedJson) ? connectedJson : (connectedJson.devices || []);
      const available = (devicesJson.available ?? connectedJson.available ?? true);

      statusEl.textContent = available
        ? `Paired devices: ${pairedDevices.length}, connected devices: ${connectedDevices.length}`
        : 'Bluetooth backend unavailable.';

      const pairedEl = document.getElementById('bluetoothDevices');
      pairedEl.innerHTML = '';
      pairedDevices.forEach(device => {
        const li = document.createElement('li');
        li.textContent = `${device.MacAddress || device.macAddress || device.mac || 'unknown'} - ${device.Name || device.name || 'unknown'}`;
        pairedEl.appendChild(li);
      });

      const connectedEl = document.getElementById('bluetoothConnected');
      connectedEl.innerHTML = '';
      connectedDevices.forEach(device => {
        const li = document.createElement('li');
        li.textContent = `${device.MacAddress || device.macAddress || device.mac || 'unknown'} - ${device.Name || device.name || 'unknown'}`;
        connectedEl.appendChild(li);
      });
    } catch (e) {
      console.warn('bluetooth status failed', e);
    }
  }

  document.getElementById('bluetoothScan').addEventListener('click', async () => {
    const res = await apiPost('/bluetooth/scan', {});
    alert('Bluetooth scan: ' + res);
    await updateBluetoothStatus();
  });

  document.getElementById('bluetoothConnect').addEventListener('click', async () => {
    const mac = document.getElementById('bluetoothMac').value.trim();
    const result = await apiPostJson('/bluetooth/connect', { mac });
    alert(`Bluetooth connect: ${result.success ? 'ok' : 'failed'}`);
    await updateBluetoothStatus();
  });

  document.getElementById('bluetoothDisconnect').addEventListener('click', async () => {
    const mac = document.getElementById('bluetoothMac').value.trim();
    const result = await apiPostJson('/bluetooth/disconnect', { mac });
    alert(`Bluetooth disconnect: ${result.success ? 'ok' : 'failed'}`);
    await updateBluetoothStatus();
  });

  document.getElementById('settingsEditor').addEventListener('blur', async () => {
    try {
      if (!document.getElementById('autoSave').checked) return;
      await saveCurrentSettings({ silent: true });
    } catch (e) {}
  });

  document.getElementById('applySettings').addEventListener('click', async () => {
    const r = await fetch('/settings/apply', { method: 'POST' });
    const txt = await r.text();
    if (!r.ok || txt !== 'ok') {
      setSettingsStatus(txt || 'Apply failed', true);
      return;
    }
    setSettingsStatus('Apply requested.');
  });

  setInterval(updatePlatformStatus, 4000);
  setInterval(updateMediaStatus, 4000);
  setInterval(updateBluetoothStatus, 4000);
  setInterval(updateNetworkStatus, 4000);

  (async () => {
    try {
      const as = localStorage.getItem('hass_autosave');
      const aa = localStorage.getItem('hass_autoapply');
      document.getElementById('autoSave').checked = as === null ? true : as === '1';
      document.getElementById('autoApply').checked = aa === '1';
      if (as === null) localStorage.setItem('hass_autosave', '1');
    } catch (e) {}

    try {
      await loadCurrentSettings();
    } catch (e) {
      setSettingsStatus(`Failed to load settings: ${e.message}`, true);
    }

    await updatePlatformStatus();
    await updateMediaStatus();
    await updateBluetoothStatus();
    await updateNetworkStatus();
    document.getElementById('refreshCmds').click();
    document.getElementById('refreshSensors').click();
  })();
});
