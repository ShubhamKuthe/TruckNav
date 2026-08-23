package com.trucknav.trucknav_mobile

import android.content.Context
import android.content.pm.PackageManager
import android.net.wifi.WifiManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    private var multicastLock: WifiManager.MulticastLock? = null

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            "trucknav/local_network",
        ).setMethodCallHandler { call, result ->
            when (call.method) {
                "prepareDiscoveryAccess" -> result.success(prepareDiscoveryAccess())
                "releaseDiscoveryAccess" -> {
                    releaseDiscoveryAccess()
                    result.success(null)
                }
                else -> result.notImplemented()
            }
        }
    }

    override fun onDestroy() {
        releaseDiscoveryAccess()
        super.onDestroy()
    }

    private fun prepareDiscoveryAccess(): Map<String, Any?> {
        val state = currentLocalNetworkState().toMutableMap()
        if (state["status"] != "granted") {
            return state
        }

        val wifiManager =
            applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
        if (wifiManager == null) {
            state["status"] = "denied"
            state["message"] =
                "TruckNav could not access Android Wi-Fi multicast services on this device."
            state["multicastLockHeld"] = false
            return state
        }

        if (multicastLock == null) {
            multicastLock = wifiManager.createMulticastLock("trucknav-mdns").apply {
                setReferenceCounted(false)
            }
        }

        try {
            if (multicastLock?.isHeld != true) {
                multicastLock?.acquire()
            }
        } catch (_: SecurityException) {
            state["status"] = "denied"
            state["message"] =
                "TruckNav could not acquire Android multicast access. Verify CHANGE_WIFI_MULTICAST_STATE is present."
            state["multicastLockHeld"] = false
            return state
        }

        state["multicastLockHeld"] = multicastLock?.isHeld == true
        return state
    }

    private fun releaseDiscoveryAccess() {
        val lock = multicastLock ?: return
        if (lock.isHeld) {
            lock.release()
        }
    }

    private fun currentLocalNetworkState(): Map<String, Any?> {
        val targetSdkVersion = applicationInfo.targetSdkVersion
        val requiresRuntimePermission = targetSdkVersion >= 37
        val permissionName = "android.permission.ACCESS_LOCAL_NETWORK"

        if (!requiresRuntimePermission) {
            return mapOf(
                "status" to "granted",
                "message" to null,
                "multicastLockHeld" to (multicastLock?.isHeld == true),
                "targetSdkVersion" to targetSdkVersion,
                "requiresRuntimePermission" to false,
            )
        }

        val permissionGranted =
            checkSelfPermission(permissionName) == PackageManager.PERMISSION_GRANTED

        return mapOf(
            "status" to if (permissionGranted) "granted" else "denied",
            "message" to if (permissionGranted) {
                null
            } else {
                "Local network access is required before TruckNav can discover or connect to a LAN Connector."
            },
            "multicastLockHeld" to (multicastLock?.isHeld == true),
            "targetSdkVersion" to targetSdkVersion,
            "requiresRuntimePermission" to true,
        )
    }
}
