// Bound bytes while streaming, including requests without Content-Length.
export async function readSigningBody(stream, maximum = 98304) {
  if (!stream) throw new Error('invalid_request');
  const reader = stream.getReader();
  const chunks = [];
  let length = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      length += value.byteLength;
      if (length > maximum) {
        value.fill(0);
        await reader.cancel();
        throw new Error('request_too_large');
      }
      chunks.push(value);
    }
    const bytes = new Uint8Array(length);
    let offset = 0;
    for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
    return bytes;
  } finally {
    for (const chunk of chunks) chunk.fill(0);
    reader.releaseLock();
  }
}
