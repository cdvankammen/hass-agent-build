async function apiPost(path, body) {
  const resp = await fetch(path, {method: 'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(body)});
  return resp.text();
}

document.addEventListener('DOMContentLoaded', () => {
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
        await apiPost('/command', c);
        alert('Command executed');
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
    await apiPost('/command', { Id: Guid(), Name: name, Execute: exec });
    alert('Custom command sent');
  });

  function Guid() { return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) { var r = Math.random()*16|0, v = c=='x'?r:(r&0x3|0x8); return v.toString(16); }); }

  // settings editor
  async function loadSettingsList() {
    const r = await fetch('/settings');
    const list = await r.json();
    const sel = document.getElementById('settingsSelect');
    sel.innerHTML = '';
    list.forEach(item => {
      const opt = document.createElement('option');
      opt.value = item.id;
      opt.textContent = item.name;
      opt.dataset.path = item.path;
      sel.appendChild(opt);
    });
  }

  document.getElementById('loadSettings').addEventListener('click', async () => {
    const sel = document.getElementById('settingsSelect');
    const fileId = sel.value;
    if (!fileId) { alert('Select a file first'); return; }
    const r = await fetch(`/settings/${fileId}`);
    const json = await r.json();
    document.getElementById('settingsPath').textContent = json.path;
    document.getElementById('settingsEditor').value = json.content ? JSON.stringify(json.content, null, 2) : '';
  });

  // initialize autosave/autapply from localStorage (defaults off)
  try {
    const as = localStorage.getItem('hass_autosave');
    const aa = localStorage.getItem('hass_autoapply');
    // default autosave ON if not set, autoapply default OFF
    document.getElementById('autoSave').checked = as === null ? true : as === '1';
    document.getElementById('autoApply').checked = aa === '1';
    // ensure localStorage has the default value for autosave
    if (as === null) localStorage.setItem('hass_autosave', '1');
  } catch (e) { }

  document.getElementById('saveSettings').addEventListener('click', async () => {
    const sel = document.getElementById('settingsSelect');
    const fileId = sel.value;
    if (!fileId) { alert('Select a file first'); return; }
    const body = document.getElementById('settingsEditor').value;
    const res = await fetch(`/settings/${fileId}`, { method: 'POST', headers: {'Content-Type':'application/json'}, body });
    const txt = await res.text();
    alert('Save result: ' + txt);
    // reload list to reflect path
    await loadSettingsList();
    if (document.getElementById('autoApply').checked) {
      await fetch('/settings/apply', { method: 'POST' });
    }
    try { localStorage.setItem('hass_autosave', document.getElementById('autoSave').checked ? '1' : '0'); localStorage.setItem('hass_autoapply', document.getElementById('autoApply').checked ? '1' : '0'); } catch (e) {}
  });

  // autosave debounce
  let autosaveTimer = null;
  document.getElementById('settingsEditor').addEventListener('input', () => {
    if (!document.getElementById('autoSave').checked) return;
    if (autosaveTimer) clearTimeout(autosaveTimer);
    autosaveTimer = setTimeout(async () => {
      const sel = document.getElementById('settingsSelect');
      const fileId = sel.value;
      if (!fileId) return;
      const body = document.getElementById('settingsEditor').value;
      await fetch(`/settings/${fileId}`, { method: 'POST', headers: {'Content-Type':'application/json'}, body });
      if (document.getElementById('autoApply').checked) await fetch('/settings/apply', { method: 'POST' });
    }, 800);
  });

  // persist checkboxes on change
  document.getElementById('autoSave').addEventListener('change', () => { try { localStorage.setItem('hass_autosave', document.getElementById('autoSave').checked ? '1' : '0'); } catch(e){} });
  document.getElementById('autoApply').addEventListener('change', () => { try { localStorage.setItem('hass_autoapply', document.getElementById('autoApply').checked ? '1' : '0'); } catch(e){} });

  // populate on startup
  await loadSettingsList();

  document.getElementById('applySettings').addEventListener('click', async () => {
    const r = await fetch('/settings/apply', { method: 'POST' });
    const txt = await r.text();
    alert('Apply result: ' + txt);
  });

  // SSE for external file changes (reload editor if changed)
  try {
    const es = new EventSource('/settings/stream');
    es.onmessage = function(e) {
      // data: ChangeType:filename
      const d = e.data.split(':');
      const fname = d[1];
      const sel = document.getElementById('settingsSelect');
      const opt = Array.from(sel.options).find(o => o.dataset.path && o.dataset.path.endsWith(fname));
      if (!opt) return;
      const fileId = opt.value;
      // if currently editing that file, reload content
      const currentPath = document.getElementById('settingsPath').textContent;
      if (currentPath && currentPath.endsWith(fname)) {
        // only reload if not dirty or if autoSave enabled
        if (document.getElementById('autoSave').checked) {
          // save current content first
          fetch(`/settings/${fileId}`, { method: 'POST', headers: {'Content-Type':'application/json'}, body: document.getElementById('settingsEditor').value });
        }
        // load latest
        fetch(`/settings/${fileId}`).then(r => r.json()).then(json => {
          document.getElementById('settingsEditor').value = json.content ? JSON.stringify(json.content, null, 2) : '';
          document.getElementById('settingsPath').textContent = json.path;
        }).catch(()=>{});
      }
    };
  } catch (e) { console.warn('SSE unavailable', e); }

  // poll platform status
  async function updatePlatformStatus() {
    try {
      const r = await fetch('/platform/status');
      const j = await r.json();
      const el = document.getElementById('platformStatus');
      el.textContent = `MPRIS(DBus): ${j.mprisDbus}, BlueZ(DBus): ${j.bluezDbus}, playerctl: ${j.playerctl}, bluetoothctl: ${j.btctl}, mediaEffective: ${j.effectiveMedia}`;
    } catch (e) { console.warn('platform status failed', e); }
  }
  setInterval(updatePlatformStatus, 4000);
  updatePlatformStatus();
});
