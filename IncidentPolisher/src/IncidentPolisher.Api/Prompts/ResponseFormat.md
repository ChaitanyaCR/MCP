Output instructions:

Return one valid JSON object with exactly these camelCase properties:
- "summary": a non-empty string with a concise, factual overview.
- "status": a non-empty string describing the current service/incident state
  supported by the note. Use "Unknown" if the current state cannot be determined.
  Use "Monitoring" only when the input supports monitoring, and "Resolved" only
  when the input explicitly states that the incident is resolved.
- "update": a non-empty string containing the professional, audience-appropriate
  status update.
- "missingInformation": an array of strings identifying relevant information
  missing or explicitly unknown in the note, such as customer impact or root
  cause. Describe gaps rather than filling them with guesses. Return [] if no
  relevant gaps are identified. Do not label supplied information as missing.

All four properties are required. Do not return null values, additional keys,
Markdown code fences, or text outside the JSON object. Apply the factuality and
audience rules to every field, including summary, status, and missingInformation.
