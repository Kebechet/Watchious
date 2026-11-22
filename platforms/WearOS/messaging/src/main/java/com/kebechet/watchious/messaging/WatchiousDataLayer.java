package com.kebechet.watchious.messaging;

import android.content.Context;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.gms.tasks.Task;
import com.google.android.gms.tasks.Tasks;
import com.google.android.gms.wearable.DataClient;
import com.google.android.gms.wearable.DataEvent;
import com.google.android.gms.wearable.DataEventBuffer;
import com.google.android.gms.wearable.DataMap;
import com.google.android.gms.wearable.DataMapItem;
import com.google.android.gms.wearable.MessageClient;
import com.google.android.gms.wearable.MessageEvent;
import com.google.android.gms.wearable.Node;
import com.google.android.gms.wearable.PutDataMapRequest;
import com.google.android.gms.wearable.PutDataRequest;
import com.google.android.gms.wearable.Wearable;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

// The Wearable Data Layer for Watchious.Messaging, which .NET binds. Only platform and Java types appear in its
// public API: the Play Services listener interfaces are implemented by private fields, because the .NET binding
// generator drops a public class that implements types it can't see.
public final class WatchiousDataLayer {
    public interface Listener {
        void onMessage(@NonNull String path, @NonNull byte[] data);

        void onDataItem(@NonNull String path, @NonNull String key, @Nullable String value);
    }

    public interface Callback {
        // error is null on success.
        void onComplete(@Nullable String error);
    }

    public interface ReachabilityCallback {
        void onResult(boolean isReachable);
    }

    private final Context context;
    private final Listener listener;
    private final MessageClient.OnMessageReceivedListener messageListener;
    private final DataClient.OnDataChangedListener dataListener;

    public WatchiousDataLayer(@NonNull Context context, @NonNull Listener listener) {
        this.context = context.getApplicationContext();
        this.listener = listener;
        this.messageListener = this::onMessageReceived;
        this.dataListener = this::onDataChanged;
    }

    public void start() {
        Wearable.getMessageClient(context).addListener(messageListener);
        Wearable.getDataClient(context).addListener(dataListener);
    }

    public void stop() {
        Wearable.getMessageClient(context).removeListener(messageListener);
        Wearable.getDataClient(context).removeListener(dataListener);
    }

    public void isPhoneReachable(@NonNull ReachabilityCallback callback) {
        Wearable.getNodeClient(context).getConnectedNodes().addOnCompleteListener(task ->
            callback.onResult(task.isSuccessful() && task.getResult() != null && !task.getResult().isEmpty()));
    }

    // Sends to every connected node; a watch is normally connected to one phone.
    public void sendMessage(@NonNull String path, @NonNull byte[] data, @NonNull Callback callback) {
        MessageClient messageClient = Wearable.getMessageClient(context);
        Wearable.getNodeClient(context).getConnectedNodes().addOnCompleteListener(nodesTask -> {
            if (!nodesTask.isSuccessful() || nodesTask.getResult() == null) {
                callback.onComplete(describe(nodesTask.getException(), "The connected phone couldn't be found."));
                return;
            }

            List<Node> nodes = nodesTask.getResult();
            if (nodes.isEmpty()) {
                callback.onComplete("No phone is connected.");
                return;
            }

            List<Task<Integer>> sends = new ArrayList<>();
            for (Node node : nodes) {
                sends.add(messageClient.sendMessage(node.getId(), path, data));
            }
            Tasks.whenAll(sends).addOnCompleteListener(sendTask ->
                callback.onComplete(sendTask.isSuccessful() ? null : describe(sendTask.getException(), "The message wasn't sent.")));
        });
    }

    public void putDataItem(@NonNull String path, @NonNull Map<String, String> values, @NonNull Callback callback) {
        PutDataMapRequest request = PutDataMapRequest.create(path);
        DataMap dataMap = request.getDataMap();
        for (Map.Entry<String, String> value : values.entrySet()) {
            dataMap.putString(value.getKey(), value.getValue());
        }
        PutDataRequest putRequest = request.asPutDataRequest().setUrgent();
        Wearable.getDataClient(context).putDataItem(putRequest).addOnCompleteListener(task ->
            callback.onComplete(task.isSuccessful() ? null : describe(task.getException(), "The data item wasn't saved.")));
    }

    private void onMessageReceived(@NonNull MessageEvent messageEvent) {
        listener.onMessage(messageEvent.getPath(), messageEvent.getData());
    }

    private void onDataChanged(@NonNull DataEventBuffer dataEvents) {
        try {
            for (DataEvent dataEvent : dataEvents) {
                if (dataEvent.getType() != DataEvent.TYPE_CHANGED) {
                    continue;
                }
                String path = dataEvent.getDataItem().getUri().getPath();
                DataMap dataMap = DataMapItem.fromDataItem(dataEvent.getDataItem()).getDataMap();
                for (String key : dataMap.keySet()) {
                    Object value = dataMap.get(key);
                    listener.onDataItem(path == null ? "" : path, key, value == null ? null : value.toString());
                }
            }
        } finally {
            dataEvents.release();
        }
    }

    @NonNull
    private static String describe(@Nullable Exception exception, @NonNull String fallback) {
        return exception == null || exception.getMessage() == null ? fallback : exception.getMessage();
    }
}
