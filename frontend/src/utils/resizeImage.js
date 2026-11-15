const acceptedTypes = new Set(['image/jpeg', 'image/png', 'image/webp']);

export async function resizeImageToDataUrl(file, maxDimension) {
  if (!acceptedTypes.has(file.type)) {
    throw new Error('Choose a JPEG, PNG, or WebP image.');
  }
  if (file.size > 5 * 1024 * 1024) {
    throw new Error('Choose an image smaller than 5 MB.');
  }

  const image = await createImageBitmap(file);
  try {
    const scale = Math.min(1, maxDimension / Math.max(image.width, image.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(image.width * scale));
    canvas.height = Math.max(1, Math.round(image.height * scale));
    const context = canvas.getContext('2d');
    if (!context) throw new Error('This browser could not prepare the image.');
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL('image/webp', 0.82);
  } finally {
    image.close();
  }
}
