import os
import json
import sqlite3
import requests
import logging
import traceback

# --- Configuration & Path Setup ---

# Dynamically determine company_id based on the folder name
current_dir = os.path.dirname(os.path.abspath(__file__))
company_id = os.path.basename(current_dir)

# Set up logging to {company_id}_history.log in the same directory
log_file = os.path.join(current_dir, f"{company_id}_history.log")
logging.basicConfig(
    filename=log_file,
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)

def run_sync():
    logging.info(f"=== Starting Sync Job for Tenant: {company_id} ===")
    
    config_filename = f"{company_id}_config.json"
    db_filename = f"{company_id}.db"
    
    config_path = os.path.join(current_dir, config_filename)
    db_path = os.path.join(current_dir, db_filename)
    
    # Verify configuration exists
    if not os.path.exists(config_path):
        logging.error(f"Configuration file not found: {config_path}")
        return

    conn = None
    try:
        # Read the JSON config file
        with open(config_path, 'r') as f:
            config = json.load(f)
        
        sync_urls = config.get("sync_urls", {})
        if not sync_urls:
            logging.warning(f"No 'sync_urls' found in {config_filename}. Nothing to sync.")
            return

        # Connect to the local SQLite database
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()
        
        for table_name, url in sync_urls.items():
            logging.info(f"Processing table: {table_name} from URL: {url}")
            
            try:
                # Fetch remote JSON data
                response = requests.get(url, timeout=60)
                response.raise_for_status()
                dataset = response.json()
                
                if not dataset or not isinstance(dataset, list):
                    logging.warning(f"Skipping {table_name}: Payload is empty or not a list.")
                    continue
                
                # Dynamically generate INSERT statement based on the first record
                first_record = dataset[0]
                columns = list(first_record.keys())
                placeholders = ", ".join(["?" for _ in columns])
                col_names = ", ".join(columns)
                insert_sql = f"INSERT INTO {table_name} ({col_names}) VALUES ({placeholders})"
                
                # Execute Wipe and Replace in a sub-transaction
                # We use a try block here to ensure one table failure doesn't stop others
                try:
                    # Create table if it doesn't exist
                    create_cols = []
                    for k, v in first_record.items():
                        if isinstance(v, int):
                            create_cols.append(f"{k} INTEGER")
                        elif isinstance(v, float):
                            create_cols.append(f"{k} REAL")
                        else:
                            create_cols.append(f"{k} TEXT")
                    cursor.execute(f"CREATE TABLE IF NOT EXISTS {table_name} ({', '.join(create_cols)})")

                    # Clear existing data
                    cursor.execute(f"DELETE FROM {table_name}")
                    
                    # Prepare data for bulk insertion
                    bulk_data = [tuple(row.get(col) for col in columns) for row in dataset]
                    
                    # Perform bulk insert
                    cursor.executemany(insert_sql, bulk_data)
                    logging.info(f"Successfully replaced {len(bulk_data)} rows in {table_name}.")
                    
                except sqlite3.Error as db_err:
                    logging.error(f"Database error while replacing {table_name}: {str(db_err)}")
                    # Individual table failures are logged but we continue to the next table
                    
            except Exception as e:
                logging.error(f"Failed to fetch or process data for {table_name}: {str(e)}")
                logging.error(traceback.format_exc())

        # Commit all successful changes
        conn.commit()
        logging.info(f"=== Sync Job Completed for Tenant: {company_id} ===")

    except Exception as e:
        logging.critical(f"Critical error in sync job: {str(e)}")
        logging.error(traceback.format_exc())
        if conn:
            conn.rollback()
    finally:
        if conn:
            conn.close()
            logging.info("Database connection closed.")

if __name__ == "__main__":
    run_sync()
