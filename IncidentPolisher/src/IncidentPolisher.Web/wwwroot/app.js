const form = document.querySelector('#polish-form');
const submit = document.querySelector('#submit');
const message = document.querySelector('#message');
const result = document.querySelector('#result');
const output = document.querySelector('#output');

document.querySelector('#sample').addEventListener('click', () => {
  document.querySelector('#incident').value = 'Temporary degradation observed in payment service. Mitigation applied; monitoring now. Exact customer impact is unknown. Root cause has not been provided.';
  document.querySelector('#incident').focus();
});

form.addEventListener('submit', async event => {
  event.preventDefault();
  const incidentNote = document.querySelector('#incident').value.trim();
  if (!incidentNote) {
    message.textContent = 'Enter an incident note before submitting.';
    document.querySelector('#incident').focus();
    return;
  }
  submit.disabled = true;
  submit.textContent = 'Polishing…';
  output.setAttribute('aria-busy', 'true');
  result.hidden = true;
  message.classList.remove('error');
  message.textContent = 'Preparing your update…';
  try {
    const response = await fetch('/api/incidents/polish', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ incidentNote, audience: document.querySelector('#audience').value })
    });
    const data = await response.json();
    if (!response.ok) {
      const validation = data.errors ? Object.values(data.errors).flat().join(' ') : '';
      throw new Error(validation || data.detail || data.title || 'The request failed. Try again.');
    }
    for (const field of ['summary', 'status', 'update']) {
      document.querySelector(`#${field}`).textContent = data[field];
    }
    const missing = document.querySelector('#missing');
    missing.replaceChildren();
    for (const item of data.missingInformation) {
      const li = document.createElement('li');
      li.textContent = item;
      missing.append(li);
    }
    document.querySelector('#no-missing').hidden = data.missingInformation.length !== 0;
    message.textContent = 'Update ready.';
    result.hidden = false;
  } catch (error) {
    message.classList.add('error');
    message.textContent = error.message || 'Could not submit the incident. Try again.';
  } finally {
    submit.disabled = false;
    submit.textContent = 'Polish incident';
    output.setAttribute('aria-busy', 'false');
  }
});
