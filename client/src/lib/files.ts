/**
 * Showing and saving files the API makes - invoice PDFs, report PDFs, Excel sheets. The file is
 * fetched with the session cookie (through `api.blob`), then handed to the browser.
 */

/** Opens a file in a new tab. The tab is opened first, inside the click, so no pop-up blocker objects. */
export async function openInNewTab(getFile: () => Promise<Blob>) {
  const tab = window.open('', '_blank')

  try {
    const url = URL.createObjectURL(await getFile())

    if (tab) {
      tab.location.href = url
    } else {
      window.location.href = url
    }

    // The tab keeps its own copy once loaded.
    setTimeout(() => URL.revokeObjectURL(url), 60_000)
  } catch (caught) {
    tab?.close()
    throw caught
  }
}

export async function saveFile(getFile: () => Promise<Blob>, fileName: string) {
  const url = URL.createObjectURL(await getFile())
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
