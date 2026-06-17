# Chrome DevTools Protocol (CDP) Compliance Report

Generated on: 2026-06-17 21:39:59 UTC

This report lists the level of compliance of the `Avalonia.Diagnostics.Cdp` library against the official Chrome DevTools Protocol specification.

## Summary

* **Total Standard CDP Methods Supported**: 50 / 668 (7,5%)
* **Total Custom/Extension Methods Supported**: 53

| Domain | Status / Coverage | Standard Supported | Custom Extensions | Missing Standard |
| :--- | :--- | :--- | :--- | :--- |
| **Accessibility** | 3/8 (37,5%) | 3 | 1 | 5 |
| **Ads** | Unsupported | 0 | 0 | 1 |
| **Animation** | Unsupported | 0 | 0 | 10 |
| **Application** | Custom Domain (3 actions) | 0 | 3 | 0 |
| **Audits** | Unsupported | 0 | 0 | 4 |
| **Autofill** | Unsupported | 0 | 0 | 4 |
| **BackgroundService** | Unsupported | 0 | 0 | 4 |
| **BluetoothEmulation** | Unsupported | 0 | 0 | 15 |
| **Browser** | 2/20 (10,0%) | 2 | 0 | 18 |
| **CacheStorage** | Unsupported | 0 | 0 | 5 |
| **Cast** | Unsupported | 0 | 0 | 6 |
| **Console** | Unsupported | 0 | 0 | 3 |
| **CrashReportContext** | Unsupported | 0 | 0 | 1 |
| **Css** | Custom Domain (5 actions) | 0 | 5 | 0 |
| **CSS** | Unsupported | 0 | 0 | 39 |
| **Debugger** | Unsupported | 0 | 0 | 33 |
| **DeviceAccess** | Unsupported | 0 | 0 | 4 |
| **DeviceOrientation** | Unsupported | 0 | 0 | 2 |
| **Dom** | Custom Domain (18 actions) | 0 | 18 | 0 |
| **DOM** | Unsupported | 0 | 0 | 53 |
| **DomDebugger** | Custom Domain (1 actions) | 0 | 1 | 0 |
| **DOMDebugger** | Unsupported | 0 | 0 | 10 |
| **DOMSnapshot** | Unsupported | 0 | 0 | 4 |
| **DOMStorage** | Unsupported | 0 | 0 | 6 |
| **Emulation** | 4/47 (8,5%) | 4 | 1 | 43 |
| **EventBreakpoints** | Unsupported | 0 | 0 | 3 |
| **Extensions** | Unsupported | 0 | 0 | 8 |
| **FedCm** | Unsupported | 0 | 0 | 7 |
| **Fetch** | Unsupported | 0 | 0 | 9 |
| **FileSystem** | Unsupported | 0 | 0 | 1 |
| **HeadlessExperimental** | Unsupported | 0 | 0 | 3 |
| **HeapProfiler** | Unsupported | 0 | 0 | 12 |
| **IndexedDB** | Unsupported | 0 | 0 | 9 |
| **Input** | 3/13 (23,1%) | 3 | 18 | 10 |
| **Inspector** | Unsupported | 0 | 0 | 2 |
| **IO** | Unsupported | 0 | 0 | 3 |
| **LayerTree** | Unsupported | 0 | 0 | 9 |
| **Log** | 3/5 (60,0%) | 3 | 0 | 2 |
| **Media** | Unsupported | 0 | 0 | 2 |
| **Memory** | 1/11 (9,1%) | 1 | 2 | 10 |
| **Network** | 4/41 (9,8%) | 4 | 0 | 37 |
| **Overlay** | 5/30 (16,7%) | 5 | 0 | 25 |
| **Page** | 10/61 (16,4%) | 10 | 0 | 51 |
| **Performance** | 3/4 (75,0%) | 3 | 0 | 1 |
| **PerformanceTimeline** | Unsupported | 0 | 0 | 1 |
| **Preload** | Unsupported | 0 | 0 | 2 |
| **Profiler** | Unsupported | 0 | 0 | 9 |
| **PWA** | Unsupported | 0 | 0 | 7 |
| **Recorder** | Custom Domain (2 actions) | 0 | 2 | 0 |
| **Runtime** | 8/23 (34,8%) | 8 | 0 | 15 |
| **Schema** | Unsupported | 0 | 0 | 1 |
| **Security** | Unsupported | 0 | 0 | 5 |
| **ServiceWorker** | Unsupported | 0 | 0 | 12 |
| **SmartCardEmulation** | Unsupported | 0 | 0 | 12 |
| **Sources** | Custom Domain (2 actions) | 0 | 2 | 0 |
| **Storage** | Unsupported | 0 | 0 | 34 |
| **SystemInfo** | 2/3 (66,7%) | 2 | 0 | 1 |
| **Target** | 2/19 (10,5%) | 2 | 0 | 17 |
| **Tethering** | Unsupported | 0 | 0 | 2 |
| **Tracing** | Unsupported | 0 | 0 | 6 |
| **WebAudio** | Unsupported | 0 | 0 | 3 |
| **WebAuthn** | Unsupported | 0 | 0 | 13 |
| **WebMCP** | Unsupported | 0 | 0 | 4 |

## Domain Details

### Accessibility

* **Standard Supported (3)**: `disable`, `enable`, `getFullAXTree`
* **Custom Extensions (1)**: `getAXNode`
* **Missing Standard (5)**: `getAXNodeAndAncestors`, `getChildAXNodes`, `getPartialAXTree`, `getRootAXNode`, `queryAXTree`

### Ads

* **Missing Standard (1)**: `getAdMetrics`

### Animation

* **Missing Standard (10)**: `disable`, `enable`, `getCurrentTime`, `getPlaybackRate`, `releaseAnimations`, `resolveAnimation`, `seekAnimations`, `setPaused`, `setPlaybackRate`, `setTiming`

### Application

* **Custom Extensions (3)**: `deleteResource`, `getResources`, `setResource`

### Audits

* **Missing Standard (4)**: `checkFormsIssues`, `disable`, `enable`, `getEncodedResponse`

### Autofill

* **Missing Standard (4)**: `disable`, `enable`, `setAddresses`, `trigger`

### BackgroundService

* **Missing Standard (4)**: `clearEvents`, `setRecording`, `startObserving`, `stopObserving`

### BluetoothEmulation

* **Missing Standard (15)**: `addCharacteristic`, `addDescriptor`, `addService`, `disable`, `enable`, `removeCharacteristic`, `removeDescriptor`, `removeService`, `setSimulatedCentralState`, `simulateAdvertisement`, `simulateCharacteristicOperationResponse`, `simulateDescriptorOperationResponse`, `simulateGATTDisconnection`, `simulateGATTOperationResponse`, `simulatePreconnectedPeripheral`

### Browser

* **Standard Supported (2)**: `close`, `getVersion`
* **Missing Standard (18)**: `addPrivacySandboxCoordinatorKeyConfig`, `addPrivacySandboxEnrollmentOverride`, `cancelDownload`, `crash`, `crashGpuProcess`, `executeBrowserCommand`, `getBrowserCommandLine`, `getHistogram`, `getHistograms`, `getWindowBounds`, `getWindowForTarget`, `grantPermissions`, `resetPermissions`, `setContentsSize`, `setDockTile`, `setDownloadBehavior`, `setPermission`, `setWindowBounds`

### CacheStorage

* **Missing Standard (5)**: `deleteCache`, `deleteEntry`, `requestCachedResponse`, `requestCacheNames`, `requestEntries`

### Cast

* **Missing Standard (6)**: `disable`, `enable`, `setSinkToUse`, `startDesktopMirroring`, `startTabMirroring`, `stopCasting`

### Console

* **Missing Standard (3)**: `clearMessages`, `disable`, `enable`

### CrashReportContext

* **Missing Standard (1)**: `getEntries`

### Css

* **Custom Extensions (5)**: `disable`, `enable`, `getComputedStyleForNode`, `getMatchedStylesForNode`, `setStyleTexts`

### CSS

* **Missing Standard (39)**: `addRule`, `collectClassNames`, `createStyleSheet`, `disable`, `enable`, `forcePseudoState`, `forceStartingStyle`, `getAnimatedStylesForNode`, `getBackgroundColors`, `getComputedStyleForNode`, `getEnvironmentVariables`, `getInlineStylesForNode`, `getLayersForNode`, `getLocationForSelector`, `getLonghandProperties`, `getMatchedStylesForNode`, `getMediaQueries`, `getPlatformFontsForNode`, `getStyleSheetText`, `resolveValues`, `setContainerQueryConditionText`, `setContainerQueryText`, `setEffectivePropertyValueForNode`, `setKeyframeKey`, `setLocalFontsEnabled`, `setMediaText`, `setNavigationText`, `setPropertyRulePropertyName`, `setRuleSelector`, `setScopeText`, `setStyleSheetText`, `setStyleTexts`, `setSupportsText`, `startRuleUsageTracking`, `stopRuleUsageTracking`, `takeComputedStyleUpdates`, `takeCoverageDelta`, `trackComputedStyleUpdates`, `trackComputedStyleUpdatesForNode`

### Debugger

* **Missing Standard (33)**: `continueToLocation`, `disable`, `disassembleWasmModule`, `enable`, `evaluateOnCallFrame`, `getPossibleBreakpoints`, `getScriptSource`, `getStackTrace`, `getWasmBytecode`, `nextWasmDisassemblyChunk`, `pause`, `pauseOnAsyncCall`, `removeBreakpoint`, `restartFrame`, `resume`, `searchInContent`, `setAsyncCallStackDepth`, `setBlackboxedRanges`, `setBlackboxExecutionContexts`, `setBlackboxPatterns`, `setBreakpoint`, `setBreakpointByUrl`, `setBreakpointOnFunctionCall`, `setBreakpointsActive`, `setInstrumentationBreakpoint`, `setPauseOnExceptions`, `setReturnValue`, `setScriptSource`, `setSkipAllPauses`, `setVariableValue`, `stepInto`, `stepOut`, `stepOver`

### DeviceAccess

* **Missing Standard (4)**: `cancelPrompt`, `disable`, `enable`, `selectPrompt`

### DeviceOrientation

* **Missing Standard (2)**: `clearDeviceOrientationOverride`, `setDeviceOrientationOverride`

### Dom

* **Custom Extensions (18)**: `disable`, `discardSearchResults`, `enable`, `focus`, `getBoxModel`, `getDocument`, `getNodeForLocation`, `getOuterHTML`, `getSearchResults`, `performSearch`, `querySelector`, `querySelectorAll`, `removeAttribute`, `removeNode`, `requestChildNodes`, `resolveNode`, `setAttributeValue`, `setInspectedNode`

### DOM

* **Missing Standard (53)**: `collectClassNamesFromSubtree`, `copyTo`, `describeNode`, `disable`, `discardSearchResults`, `enable`, `focus`, `forceShowPopover`, `getAnchorElement`, `getAttributes`, `getBoxModel`, `getContainerForNode`, `getContentQuads`, `getDetachedDomNodes`, `getDocument`, `getElementByRelation`, `getFileInfo`, `getFlattenedDocument`, `getFrameOwner`, `getNodeForLocation`, `getNodesForSubtreeByStyle`, `getNodeStackTraces`, `getOuterHTML`, `getQueryingDescendantsForContainer`, `getRelayoutBoundary`, `getSearchResults`, `getTopLayerElements`, `hideHighlight`, `highlightNode`, `highlightRect`, `markUndoableState`, `moveTo`, `performSearch`, `pushNodeByPathToFrontend`, `pushNodesByBackendIdsToFrontend`, `querySelector`, `querySelectorAll`, `redo`, `removeAttribute`, `removeNode`, `requestChildNodes`, `requestNode`, `resolveNode`, `scrollIntoViewIfNeeded`, `setAttributesAsText`, `setAttributeValue`, `setFileInputFiles`, `setInspectedNode`, `setNodeName`, `setNodeStackTracesEnabled`, `setNodeValue`, `setOuterHTML`, `undo`

### DomDebugger

* **Custom Extensions (1)**: `getEventListeners`

### DOMDebugger

* **Missing Standard (10)**: `getEventListeners`, `removeDOMBreakpoint`, `removeEventListenerBreakpoint`, `removeInstrumentationBreakpoint`, `removeXHRBreakpoint`, `setBreakOnCSPViolation`, `setDOMBreakpoint`, `setEventListenerBreakpoint`, `setInstrumentationBreakpoint`, `setXHRBreakpoint`

### DOMSnapshot

* **Missing Standard (4)**: `captureSnapshot`, `disable`, `enable`, `getSnapshot`

### DOMStorage

* **Missing Standard (6)**: `clear`, `disable`, `enable`, `getDOMStorageItems`, `removeDOMStorageItem`, `setDOMStorageItem`

### Emulation

* **Standard Supported (4)**: `clearDeviceMetricsOverride`, `setDeviceMetricsOverride`, `setEmulatedMedia`, `setLocaleOverride`
* **Custom Extensions (1)**: `setEmulatedColorSchemeOverride`
* **Missing Standard (43)**: `addScreen`, `canEmulate`, `clearDevicePostureOverride`, `clearDisplayFeaturesOverride`, `clearGeolocationOverride`, `clearIdleOverride`, `getOverriddenSensorInformation`, `getScreenInfos`, `removeScreen`, `resetPageScaleFactor`, `setAutoDarkModeOverride`, `setAutomationOverride`, `setCPUThrottlingRate`, `setDataSaverOverride`, `setDefaultBackgroundColorOverride`, `setDevicePostureOverride`, `setDisabledImageTypes`, `setDisplayFeaturesOverride`, `setDocumentCookieDisabled`, `setEmitTouchEventsForMouse`, `setEmulatedOSTextScale`, `setEmulatedVisionDeficiency`, `setFocusEmulationEnabled`, `setGeolocationOverride`, `setHardwareConcurrencyOverride`, `setIdleOverride`, `setNavigatorOverrides`, `setPageScaleFactor`, `setPressureSourceOverrideEnabled`, `setPressureStateOverride`, `setPrimaryScreen`, `setSafeAreaInsetsOverride`, `setScriptExecutionDisabled`, `setScrollbarsHidden`, `setSensorOverrideEnabled`, `setSensorOverrideReadings`, `setSmallViewportHeightDifferenceOverride`, `setTimezoneOverride`, `setTouchEmulationEnabled`, `setUserAgentOverride`, `setVirtualTimePolicy`, `setVisibleSize`, `updateScreen`

### EventBreakpoints

* **Missing Standard (3)**: `disable`, `removeInstrumentationBreakpoint`, `setInstrumentationBreakpoint`

### Extensions

* **Missing Standard (8)**: `clearStorageItems`, `getExtensions`, `getStorageItems`, `loadUnpacked`, `removeStorageItems`, `setStorageItems`, `triggerAction`, `uninstall`

### FedCm

* **Missing Standard (7)**: `clickDialogButton`, `disable`, `dismissDialog`, `enable`, `openUrl`, `resetCooldown`, `selectAccount`

### Fetch

* **Missing Standard (9)**: `continueRequest`, `continueResponse`, `continueWithAuth`, `disable`, `enable`, `failRequest`, `fulfillRequest`, `getResponseBody`, `takeResponseBodyAsStream`

### FileSystem

* **Missing Standard (1)**: `getDirectory`

### HeadlessExperimental

* **Missing Standard (3)**: `beginFrame`, `disable`, `enable`

### HeapProfiler

* **Missing Standard (12)**: `addInspectedHeapObject`, `collectGarbage`, `disable`, `enable`, `getHeapObjectId`, `getObjectByHeapObjectId`, `getSamplingProfile`, `startSampling`, `startTrackingHeapObjects`, `stopSampling`, `stopTrackingHeapObjects`, `takeHeapSnapshot`

### IndexedDB

* **Missing Standard (9)**: `clearObjectStore`, `deleteDatabase`, `deleteObjectStoreEntries`, `disable`, `enable`, `getMetadata`, `requestData`, `requestDatabase`, `requestDatabaseNames`

### Input

* **Standard Supported (3)**: `dispatchKeyEvent`, `dispatchMouseEvent`, `insertText`
* **Custom Extensions (18)**: `arrowdown`, `arrowleft`, `arrowright`, `arrowup`, `backspace`, `delete`, `down`, `end`, `enter`, `escape`, `home`, `left`, `pagedown`, `pageup`, `right`, `space`, `tab`, `up`
* **Missing Standard (10)**: `cancelDragging`, `dispatchDragEvent`, `dispatchTouchEvent`, `emulateTouchFromMouseEvent`, `imeSetComposition`, `setIgnoreInputEvents`, `setInterceptDrags`, `synthesizePinchGesture`, `synthesizeScrollGesture`, `synthesizeTapGesture`

### Inspector

* **Missing Standard (2)**: `disable`, `enable`

### IO

* **Missing Standard (3)**: `close`, `read`, `resolveBlob`

### LayerTree

* **Missing Standard (9)**: `compositingReasons`, `disable`, `enable`, `loadSnapshot`, `makeSnapshot`, `profileSnapshot`, `releaseSnapshot`, `replaySnapshot`, `snapshotCommandLog`

### Log

* **Standard Supported (3)**: `clear`, `disable`, `enable`
* **Missing Standard (2)**: `startViolationsReport`, `stopViolationsReport`

### Media

* **Missing Standard (2)**: `disable`, `enable`

### Memory

* **Standard Supported (1)**: `getDOMCounters`
* **Custom Extensions (2)**: `collectGarbage`, `getLiveControls`
* **Missing Standard (10)**: `forciblyPurgeJavaScriptMemory`, `getAllTimeSamplingProfile`, `getBrowserSamplingProfile`, `getDOMCountersForLeakDetection`, `getSamplingProfile`, `prepareForLeakDetection`, `setPressureNotificationsSuppressed`, `simulatePressureNotification`, `startSampling`, `stopSampling`

### Network

* **Standard Supported (4)**: `disable`, `emulateNetworkConditions`, `enable`, `getResponseBody`
* **Missing Standard (37)**: `canClearBrowserCache`, `canClearBrowserCookies`, `canEmulateNetworkConditions`, `clearAcceptedEncodingsOverride`, `clearBrowserCache`, `clearBrowserCookies`, `configureDurableMessages`, `continueInterceptedRequest`, `deleteCookies`, `deleteDeviceBoundSession`, `emulateNetworkConditionsByRule`, `enableDeviceBoundSessions`, `enableReportingApi`, `fetchSchemefulSite`, `getAllCookies`, `getCertificate`, `getCookies`, `getRequestPostData`, `getResponseBodyForInterception`, `getSecurityIsolationStatus`, `loadNetworkResource`, `overrideNetworkState`, `replayXHR`, `searchInResponseBody`, `setAcceptedEncodings`, `setAttachDebugStack`, `setBlockedURLs`, `setBypassServiceWorker`, `setCacheDisabled`, `setCookie`, `setCookieControls`, `setCookies`, `setExtraHTTPHeaders`, `setRequestInterception`, `setUserAgentOverride`, `streamResourceContent`, `takeResponseBodyForInterceptionAsStream`

### Overlay

* **Standard Supported (5)**: `disable`, `enable`, `hideHighlight`, `highlightNode`, `setInspectMode`
* **Missing Standard (25)**: `getGridHighlightObjectsForTest`, `getHighlightObjectForTest`, `getSourceOrderHighlightObjectForTest`, `highlightFrame`, `highlightQuad`, `highlightRect`, `highlightSourceOrder`, `setPausedInDebuggerMessage`, `setShowAdHighlights`, `setShowContainerQueryOverlays`, `setShowDebugBorders`, `setShowFlexOverlays`, `setShowFPSCounter`, `setShowGridOverlays`, `setShowHinge`, `setShowHitTestBorders`, `setShowInspectedElementAnchor`, `setShowIsolatedElements`, `setShowLayoutShiftRegions`, `setShowPaintRects`, `setShowScrollBottleneckRects`, `setShowScrollSnapOverlays`, `setShowViewportSizeOnResize`, `setShowWebVitals`, `setShowWindowControlsOverlay`

### Page

* **Standard Supported (10)**: `captureScreenshot`, `disable`, `enable`, `getResourceContent`, `getResourceTree`, `navigate`, `reload`, `screencastFrameAck`, `startScreencast`, `stopScreencast`
* **Missing Standard (51)**: `addCompilationCache`, `addScriptToEvaluateOnLoad`, `addScriptToEvaluateOnNewDocument`, `bringToFront`, `captureSnapshot`, `clearCompilationCache`, `clearDeviceMetricsOverride`, `clearDeviceOrientationOverride`, `clearGeolocationOverride`, `close`, `crash`, `createIsolatedWorld`, `deleteCookie`, `generateTestReport`, `getAdScriptAncestry`, `getAnnotatedPageContent`, `getAppId`, `getAppManifest`, `getFrameTree`, `getInstallabilityErrors`, `getLayoutMetrics`, `getManifestIcons`, `getNavigationHistory`, `getOriginTrials`, `getPermissionsPolicyState`, `handleJavaScriptDialog`, `navigateToHistoryEntry`, `printToPDF`, `produceCompilationCache`, `removeScriptToEvaluateOnLoad`, `removeScriptToEvaluateOnNewDocument`, `resetNavigationHistory`, `searchInResource`, `setAdBlockingEnabled`, `setBypassCSP`, `setDeviceMetricsOverride`, `setDeviceOrientationOverride`, `setDocumentContent`, `setDownloadBehavior`, `setFontFamilies`, `setFontSizes`, `setGeolocationOverride`, `setInterceptFileChooserDialog`, `setLifecycleEventsEnabled`, `setPrerenderingAllowed`, `setRPHRegistrationMode`, `setSPCTransactionMode`, `setTouchEmulationEnabled`, `setWebLifecycleState`, `stopLoading`, `waitForDebugger`

### Performance

* **Standard Supported (3)**: `disable`, `enable`, `getMetrics`
* **Missing Standard (1)**: `setTimeDomain`

### PerformanceTimeline

* **Missing Standard (1)**: `enable`

### Preload

* **Missing Standard (2)**: `disable`, `enable`

### Profiler

* **Missing Standard (9)**: `disable`, `enable`, `getBestEffortCoverage`, `setSamplingInterval`, `start`, `startPreciseCoverage`, `stop`, `stopPreciseCoverage`, `takePreciseCoverage`

### PWA

* **Missing Standard (7)**: `changeAppUserSettings`, `getOsAppState`, `install`, `launch`, `launchFilesInApp`, `openCurrentPageInApp`, `uninstall`

### Recorder

* **Custom Extensions (2)**: `start`, `stop`

### Runtime

* **Standard Supported (8)**: `callFunctionOn`, `disable`, `discardConsoleEntries`, `enable`, `evaluate`, `getProperties`, `releaseObject`, `releaseObjectGroup`
* **Missing Standard (15)**: `addBinding`, `awaitPromise`, `compileScript`, `getExceptionDetails`, `getHeapUsage`, `getIsolateId`, `globalLexicalScopeNames`, `queryObjects`, `removeBinding`, `runIfWaitingForDebugger`, `runScript`, `setAsyncCallStackDepth`, `setCustomObjectFormatterEnabled`, `setMaxCallStackSizeToCapture`, `terminateExecution`

### Schema

* **Missing Standard (1)**: `getDomains`

### Security

* **Missing Standard (5)**: `disable`, `enable`, `handleCertificateError`, `setIgnoreCertificateErrors`, `setOverrideCertificateErrors`

### ServiceWorker

* **Missing Standard (12)**: `deliverPushMessage`, `disable`, `dispatchPeriodicSyncEvent`, `dispatchSyncEvent`, `enable`, `setForceUpdateOnPageLoad`, `skipWaiting`, `startWorker`, `stopAllWorkers`, `stopWorker`, `unregister`, `updateRegistration`

### SmartCardEmulation

* **Missing Standard (12)**: `disable`, `enable`, `reportBeginTransactionResult`, `reportConnectResult`, `reportDataResult`, `reportError`, `reportEstablishContextResult`, `reportGetStatusChangeResult`, `reportListReadersResult`, `reportPlainResult`, `reportReleaseContextResult`, `reportStatusResult`

### Sources

* **Custom Extensions (2)**: `getFileContent`, `getWorkspaceFiles`

### Storage

* **Missing Standard (34)**: `clearCookies`, `clearDataForOrigin`, `clearDataForStorageKey`, `clearSharedStorageEntries`, `clearTrustTokens`, `deleteSharedStorageEntry`, `deleteStorageBucket`, `getCookies`, `getInterestGroupDetails`, `getRelatedWebsiteSets`, `getSharedStorageEntries`, `getSharedStorageMetadata`, `getStorageKey`, `getStorageKeyForFrame`, `getTrustTokens`, `getUsageAndQuota`, `overrideQuotaForOrigin`, `resetSharedStorageBudget`, `runBounceTrackingMitigations`, `setCookies`, `setInterestGroupAuctionTracking`, `setInterestGroupTracking`, `setProtectedAudienceKAnonymity`, `setSharedStorageEntry`, `setSharedStorageTracking`, `setStorageBucketTracking`, `trackCacheStorageForOrigin`, `trackCacheStorageForStorageKey`, `trackIndexedDBForOrigin`, `trackIndexedDBForStorageKey`, `untrackCacheStorageForOrigin`, `untrackCacheStorageForStorageKey`, `untrackIndexedDBForOrigin`, `untrackIndexedDBForStorageKey`

### SystemInfo

* **Standard Supported (2)**: `getInfo`, `getProcessInfo`
* **Missing Standard (1)**: `getFeatureState`

### Target

* **Standard Supported (2)**: `getTargets`, `setAutoAttach`
* **Missing Standard (17)**: `activateTarget`, `attachToBrowserTarget`, `attachToTarget`, `autoAttachRelated`, `closeTarget`, `createBrowserContext`, `createTarget`, `detachFromTarget`, `disposeBrowserContext`, `exposeDevToolsProtocol`, `getBrowserContexts`, `getDevToolsTarget`, `getTargetInfo`, `openDevTools`, `sendMessageToTarget`, `setDiscoverTargets`, `setRemoteLocations`

### Tethering

* **Missing Standard (2)**: `bind`, `unbind`

### Tracing

* **Missing Standard (6)**: `end`, `getCategories`, `getTrackEventDescriptor`, `recordClockSyncMarker`, `requestMemoryDump`, `start`

### WebAudio

* **Missing Standard (3)**: `disable`, `enable`, `getRealtimeData`

### WebAuthn

* **Missing Standard (13)**: `addCredential`, `addVirtualAuthenticator`, `clearCredentials`, `disable`, `enable`, `getCredential`, `getCredentials`, `removeCredential`, `removeVirtualAuthenticator`, `setAutomaticPresenceSimulation`, `setCredentialProperties`, `setResponseOverrideBits`, `setUserVerified`

### WebMCP

* **Missing Standard (4)**: `cancelInvocation`, `disable`, `enable`, `invokeTool`

